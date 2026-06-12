using Application.Features.Customer.Avatar.DTOs;
using Application.Features.Customer.Avatar.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Avatar.Commands.ExtractMeasurementsFromImage;

internal sealed class ExtractMeasurementsFromImageCommandHandler
    : IRequestHandler<ExtractMeasurementsFromImageCommand, AvatarDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBodyMeasurementExtractionService _extractionService;
    private readonly IFalAiService _falAiService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<ExtractMeasurementsFromImageCommandHandler> _logger;

    public ExtractMeasurementsFromImageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IBodyMeasurementExtractionService extractionService,
        IFalAiService falAiService,
        IFileStorageService fileStorageService,
        ILogger<ExtractMeasurementsFromImageCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _extractionService = extractionService;
        _falAiService = falAiService;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<AvatarDto> Handle(
        ExtractMeasurementsFromImageCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("Customer identity missing.");

        // 1. Validate magic bytes — reject anything that isn't JPEG or PNG.
        //    This prevents a renamed malicious file from being forwarded to the AI API.
        BodyMeasurements measurements;
        string? avatar3dModelUrl = null;

        await using (var stream = request.ImageFile.Content)
        {
            await ValidateImageMagicBytesAsync(stream, cancellationToken);

            // Reset stream position after reading the header bytes.
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            // 2. Stream the image to the AI model — image is NOT persisted.
            measurements = await _extractionService.ExtractAsync(
                stream,
                request.ImageFile.FileName,
                request.ImageFile.ContentType,
                request.HeightCm,
                cancellationToken);

            // 2.5. Generate 3D body model (best-effort — does not abort the flow on failure)
            try
            {
                // Reset stream to beginning for Cloudinary upload
                if (stream.CanSeek)
                    stream.Seek(0, SeekOrigin.Begin);

                // Upload image to Cloudinary via IFileStorageService to get a public URL for fal.ai
                var uniqueFileName = $"{customerId}_{Guid.NewGuid():N}{Path.GetExtension(request.ImageFile.FileName)}";
                var cloudinaryUrl = await _fileStorageService.UploadAsync(
                    stream, uniqueFileName, "avatars/3d-source", cancellationToken);

                _logger.LogInformation(
                    "Uploaded avatar source image for 3D generation. CustomerId: {CustomerId}, URL: {CloudinaryUrl}",
                    customerId, cloudinaryUrl);

                var bodyResult = await _falAiService.GenerateBody3dAsync(cloudinaryUrl, cancellationToken);
                avatar3dModelUrl = bodyResult.GlbUrl;

                _logger.LogInformation(
                    "3D avatar model generated successfully. CustomerId: {CustomerId}, GlbUrl: {GlbUrl}",
                    customerId, avatar3dModelUrl);
            }
            catch (ExternalServiceException ex)
            {
                _logger.LogWarning(ex,
                    "fal.ai 3D body generation failed for CustomerId {CustomerId}. Continuing without 3D model.",
                    customerId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Unexpected error during 3D body generation for CustomerId {CustomerId}. Continuing without 3D model.",
                    customerId);
            }
        }

        const string source = "AIEstimate";

        // 3. Upsert: check if the customer already has an avatar.
        var existingAvatar = await _context.Avatars
            .FirstOrDefaultAsync(a => a.CustomerId == customerId, cancellationToken);

        Domain.Entities.Customer.Avatar avatar;

        if (existingAvatar is not null)
        {
            // UPDATE existing avatar — mutate entity state directly.
            existingAvatar.UpdateMeasurements(measurements, source);

            // Apply 3D model URL if generation succeeded
            if (avatar3dModelUrl is not null)
                existingAvatar.SetAvatar3dModelUrl(avatar3dModelUrl);

            avatar = existingAvatar;
        }
        else
        {
            // CREATE new avatar
            avatar = Domain.Entities.Customer.Avatar.Create(
                customerId: customerId,
                heightCm: measurements.HeightCm,
                weightKg: measurements.WeightKg,
                chestCm: measurements.ChestCm,
                waistCm: measurements.WaistCm,
                hipsCm: measurements.HipsCm,
                shoulderWidthCm: measurements.ShoulderWidthCm,
                inseamCm: measurements.InseamCm,
                neckCm: measurements.NeckCm,
                armLengthCm: measurements.ArmLengthCm,
                shoeSizeEu: measurements.ShoeSizeEu,
                bodyShape: measurements.BodyShape,
                avatar3dModelUrl: avatar3dModelUrl);

            _context.Avatars.Add(avatar);
        }

        // 4. Record history snapshot — same approach for BOTH paths.
        //    This is committed atomically in the same SaveChangesAsync transaction.
        //    No domain events are published before save, so no side-effects leak
        //    if the transaction rolls back.
        var measurementsJson = Domain.Entities.Customer.Avatar.BuildMeasurementJson(measurements);
        var history = AvatarMeasurementHistory.CreateSnapshot(
            avatarId: avatar.Id,
            measurementDataJson: measurementsJson,
            source: source);

        _context.AvatarMeasurementHistory.Add(history);

        await _context.SaveChangesAsync(cancellationToken);

        return avatar.ToDto();
    }

    // ── Magic byte constants ─────────────────────────────────────────────────
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic  = [0x89, 0x50, 0x4E, 0x47];

    /// <summary>
    /// Reads the first 4 bytes of the stream to verify the JPEG or PNG file signature.
    /// Throws <see cref="BusinessRuleException"/> if the magic bytes do not match.
    /// The caller is responsible for resetting the stream position after this method returns.
    /// </summary>
    private static async Task ValidateImageMagicBytesAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, 4), ct);

        if (bytesRead < 3)
            throw new BusinessRuleException("INVALID_FILE_TYPE",
                "Only JPEG and PNG images are allowed. The uploaded file is too small to be a valid image.");

        // JPEG: FF D8 FF
        bool isJpeg = header[0] == JpegMagic[0]
                   && header[1] == JpegMagic[1]
                   && header[2] == JpegMagic[2];

        // PNG: 89 50 4E 47
        bool isPng = bytesRead >= 4
                  && header[0] == PngMagic[0]
                  && header[1] == PngMagic[1]
                  && header[2] == PngMagic[2]
                  && header[3] == PngMagic[3];

        if (!isJpeg && !isPng)
            throw new BusinessRuleException("INVALID_FILE_TYPE",
                "Only JPEG and PNG images are allowed. The uploaded file does not match a supported image format.");
    }
}
