using Application.Features.Customer.Avatar.DTOs;
using Application.Features.Customer.Avatar.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Avatar.Commands.RepairAvatarSourceImage;

internal sealed class RepairAvatarSourceImageCommandHandler
    : IRequestHandler<RepairAvatarSourceImageCommand, AvatarDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFalAiService _falAiService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<RepairAvatarSourceImageCommandHandler> _logger;

    public RepairAvatarSourceImageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IFalAiService falAiService,
        IFileStorageService fileStorageService,
        ICacheService cacheService,
        ILogger<RepairAvatarSourceImageCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _falAiService = falAiService;
        _fileStorageService = fileStorageService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<AvatarDto> Handle(
        RepairAvatarSourceImageCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("Customer identity missing.");

        // 1. Load the existing avatar — must exist for repair to make sense.
        var avatar = await _context.Avatars
            .FirstOrDefaultAsync(a => a.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Avatar", customerId);

        // 2. Guard: if the avatar already has a source image the customer
        //    can already do try-on.  Reject to avoid an unnecessary re-upload
        //    and accidental overwrite of a working URL.
        //    The client should check Has2DCapability (from GET /avatar) first.
        if (!string.IsNullOrWhiteSpace(avatar.SourceImageUrl))
            throw new BusinessRuleException(
                "AvatarSourceImageAlreadyExists",
                "This avatar already has a source image. " +
                "Re-create the avatar from scratch if you want to replace it.");

        // 3. Buffer the image and validate magic bytes before touching external services.
        await using var originalStream = request.FrontImageFile.Content;
        var frontBytes = await ReadAllBytesAsync(originalStream, cancellationToken);

        await using (var validateStream = new MemoryStream(frontBytes, writable: false))
            await ValidateImageMagicBytesAsync(validateStream, "front image", cancellationToken);

        // 4. Upload to storage — this MUST succeed; if it fails we throw and
        //    the avatar is left unchanged (no partial state).
        string sourceImageUrl;
        try
        {
            var uniqueFileName = $"{customerId}_{Guid.NewGuid():N}_front{Path.GetExtension(request.FrontImageFile.FileName)}";
            sourceImageUrl = await _fileStorageService.UploadAsync(
                new MemoryStream(frontBytes, writable: false),
                uniqueFileName,
                "avatars/3d-source",
                cancellationToken);

            _logger.LogInformation(
                "Repair: uploaded front image for CustomerId {CustomerId}. URL: {Url}",
                customerId, sourceImageUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Repair: failed to upload front image for CustomerId {CustomerId}.",
                customerId);
            throw new ExternalServiceException("FileStorage",
                "Failed to upload the front image. Please try again.");
        }

        // 5. Persist SourceImageUrl immediately — this is the critical path.
        //    Saving here means 2D try-on is restored even if 3D generation
        //    below times out (the bug this endpoint is designed to fix).
        avatar.SetSourceImageUrl(sourceImageUrl);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Repair: SourceImageUrl persisted for AvatarId {AvatarId}. 2D try-on now available.",
            avatar.Id);

        // 6. Optionally re-trigger 3D body-model generation (best-effort).
        //    Uses the same Polly-timeout-aware catch pattern as
        //    ExtractMeasurementsFromImageCommandHandler Phase 2.
        if (request.RetryGenerate3D && string.IsNullOrWhiteSpace(avatar.Avatar3dModelUrl))
        {
            try
            {
                var bodyResult = await _falAiService.GenerateBody3dAsync(
                    sourceImageUrl, cancellationToken);

                avatar.SetAvatar3dModelUrl(bodyResult.GlbUrl, bodyResult.FocalLength);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Repair: 3D body model generated and saved for AvatarId {AvatarId}. " +
                    "GlbUrl: {GlbUrl}",
                    avatar.Id, bodyResult.GlbUrl);
            }
            catch (ExternalServiceException ex)
            {
                _logger.LogWarning(ex,
                    "Repair: fal.ai 3D generation failed for AvatarId {AvatarId}. " +
                    "SourceImageUrl is saved; 2D try-on is available.",
                    avatar.Id);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken != cancellationToken)
            {
                // Polly internal timeout — NOT a real user cancellation.
                _logger.LogWarning(ex,
                    "Repair: fal.ai 3D generation timed out (Polly internal CT) for AvatarId {AvatarId}. " +
                    "SourceImageUrl is saved; 2D try-on is available. " +
                    "Call the endpoint again with RetryGenerate3D=true to reattempt.",
                    avatar.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Repair: unexpected error during 3D generation for AvatarId {AvatarId}. " +
                    "Continuing — 2D try-on is available.",
                    avatar.Id);
            }
        }

        // Invalidate avatar cache so the next GET /avatar returns fresh data.
        await _cacheService.RemoveAsync($"avatar:{customerId:N}", cancellationToken);

        return avatar.ToDto();
    }

    // ── Magic byte constants ──────────────────────────────────────────────────
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];

    private static async Task ValidateImageMagicBytesAsync(
        Stream stream, string imageLabel, CancellationToken ct)
    {
        var header = new byte[4];
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, 4), ct);

        if (bytesRead < 3)
            throw new BusinessRuleException("INVALID_FILE_TYPE",
                $"Only JPEG and PNG images are allowed. The uploaded {imageLabel} is too small to be a valid image.");

        bool isJpeg = header[0] == JpegMagic[0]
                   && header[1] == JpegMagic[1]
                   && header[2] == JpegMagic[2];

        bool isPng = bytesRead >= 4
                  && header[0] == PngMagic[0]
                  && header[1] == PngMagic[1]
                  && header[2] == PngMagic[2]
                  && header[3] == PngMagic[3];

        if (!isJpeg && !isPng)
            throw new BusinessRuleException("INVALID_FILE_TYPE",
                $"Only JPEG and PNG images are allowed. The uploaded {imageLabel} does not match a supported image format.");
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
