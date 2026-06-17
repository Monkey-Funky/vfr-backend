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
    private readonly ICacheService _cacheService;
    private readonly ILogger<ExtractMeasurementsFromImageCommandHandler> _logger;

    public ExtractMeasurementsFromImageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IBodyMeasurementExtractionService extractionService,
        IFalAiService falAiService,
        IFileStorageService fileStorageService,
        ICacheService cacheService,
        ILogger<ExtractMeasurementsFromImageCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _extractionService = extractionService;
        _falAiService = falAiService;
        _fileStorageService = fileStorageService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<AvatarDto> Handle(
        ExtractMeasurementsFromImageCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("Customer identity missing.");

        // 1. Buffer BOTH image byte arrays upfront so we can reuse them across
        //    multiple downstream consumers (ExtractAsync, Cloudinary uploads for 3D pipeline).
        //    Each consumer wraps the stream in StreamContent which disposes it on completion,
        //    so we give each one a fresh, disposable MemoryStream from the same byte[].
        await using var originalFrontStream = request.FrontImageFile.Content;
        await using var originalSideStream = request.SideImageFile.Content;

        var frontBytes = await ReadAllBytesAsync(originalFrontStream, cancellationToken);
        var sideBytes = await ReadAllBytesAsync(originalSideStream, cancellationToken);

        // Validate magic bytes for both images before forwarding to AI.
        await using (var validateStream = new MemoryStream(frontBytes, writable: false))
            await ValidateImageMagicBytesAsync(validateStream, "front image", cancellationToken);

        await using (var validateSideStream = new MemoryStream(sideBytes, writable: false))
            await ValidateImageMagicBytesAsync(validateSideStream, "side image", cancellationToken);

        // 2. Send both images to the AI model (uses throwaway streams from buffered bytes).
        var measurements = await _extractionService.ExtractAsync(
            new MemoryStream(frontBytes, writable: false),
            request.FrontImageFile.FileName,
            request.FrontImageFile.ContentType,
            new MemoryStream(sideBytes, writable: false),
            request.SideImageFile.FileName,
            request.SideImageFile.ContentType,
            request.HeightCm,
            cancellationToken);

        // 3. Upload the front image to get a stable public URL.
        //    This URL is required by 2D Overlay try-on (FASHN) and by the 3D SAM Align step.
        //    Best-effort: if upload fails, avatar is still saved with measurements only.
        string? sourceImageUrl = null;
        try
        {
            var uniqueFileName = $"{customerId}_{Guid.NewGuid():N}_front{Path.GetExtension(request.FrontImageFile.FileName)}";
            sourceImageUrl = await _fileStorageService.UploadAsync(
                new MemoryStream(frontBytes, writable: false), uniqueFileName, "avatars/3d-source", cancellationToken);

            _logger.LogInformation(
                "Uploaded front image as avatar source. CustomerId: {CustomerId}, URL: {CloudinaryUrl}",
                customerId, sourceImageUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Failed to upload front image to storage for CustomerId {CustomerId}. " +
                "Continuing without a source image (no try-on capability).",
                customerId);
        }

        const string source = "AIEstimate";

        // ══════════════════════════════════════════════════════════════════
        // PHASE 1 SAVE — Persist measurements + SourceImageUrl BEFORE the
        // 3D generation attempt.  This guarantees that even if fal.ai times
        // out (TaskCanceledException from Polly's internal CT) the customer's
        // avatar is written to the DB and 2D try-on remains available.
        // ══════════════════════════════════════════════════════════════════
        var existingAvatar = await _context.Avatars
            .FirstOrDefaultAsync(a => a.CustomerId == customerId, cancellationToken);

        Domain.Entities.Customer.Avatar avatar;

        if (existingAvatar is not null)
        {
            existingAvatar.UpdateMeasurements(measurements, source);

            if (sourceImageUrl is not null)
                existingAvatar.SetSourceImageUrl(sourceImageUrl);

            avatar = existingAvatar;
        }
        else
        {
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
                avatar3dModelUrl: null,        // 3D not yet generated
                avatarFocalLength: null,
                sourceImageUrl: sourceImageUrl);
            _context.Avatars.Add(avatar);
        }

        // Record history snapshot atomically with the first save.
        var measurementsJson = Domain.Entities.Customer.Avatar.BuildMeasurementJson(measurements);
        var history = AvatarMeasurementHistory.CreateSnapshot(
            avatarId: avatar.Id,
            measurementDataJson: measurementsJson,
            source: source);
        _context.AvatarMeasurementHistory.Add(history);

        // ── FIRST SaveChangesAsync ─────────────────────────────────────────
        // Measurements + SourceImageUrl are now durable.  Any failure after
        // this point does NOT roll back the customer's data.
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Phase-1 save completed. CustomerId: {CustomerId}, AvatarId: {AvatarId}, " +
            "SourceImageUrl saved: {HasSource}",
            customerId, avatar.Id, sourceImageUrl is not null);

        // ══════════════════════════════════════════════════════════════════
        // PHASE 2 — 3D body-model generation (best-effort, independent).
        //
        // BUG FIX — TaskCanceledException from Polly timeout:
        //   Polly cancels its own internal CancellationTokenSource when the
        //   configured timeout fires.  The resulting TaskCanceledException
        //   (which inherits OperationCanceledException) carries Polly's CT,
        //   NOT the caller's `cancellationToken`.
        //
        //   Previous code used `when (ex is not OperationCanceledException)`,
        //   which EXCLUDED the Polly timeout — it fell through unhandled and
        //   prevented SaveChangesAsync from ever running, leaving
        //   SourceImageUrl persisted on Cloudinary but NULL in the database.
        //
        //   Fix: catch OperationCanceledException explicitly and distinguish
        //   a real user-cancellation (same CT) from a Polly-internal timeout
        //   (different CT).  Only the latter is swallowed here; real user
        //   cancellations still propagate — but Phase-1 already wrote the
        //   avatar, so measurements + SourceImageUrl are never lost.
        // ══════════════════════════════════════════════════════════════════
        if (sourceImageUrl is not null)
        {
            string? avatar3dModelUrl = null;
            double? avatarFocalLength = null;

            try
            {
                var bodyResult = await _falAiService.GenerateBody3dAsync(sourceImageUrl, cancellationToken);
                avatar3dModelUrl = bodyResult.GlbUrl;
                avatarFocalLength = bodyResult.FocalLength;

                _logger.LogInformation(
                    "SAM 3D Body generation completed. CustomerId: {CustomerId}, " +
                    "GlbUrl: {GlbUrl}, FocalLength: {FocalLength}",
                    customerId, avatar3dModelUrl, bodyResult.FocalLength);
            }
            catch (ExternalServiceException ex)
            {
                _logger.LogWarning(ex,
                    "fal.ai 3D body generation failed for CustomerId {CustomerId}. " +
                    "2D try-on remains available via the SourceImageUrl persisted in Phase 1.",
                    customerId);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken != cancellationToken)
            {
                // Polly internal timeout — NOT a real user cancellation.
                // Swallow and continue: 2D try-on is still available.
                _logger.LogWarning(ex,
                    "fal.ai 3D body generation timed out (Polly internal CT) for CustomerId {CustomerId}. " +
                    "Avatar already saved with SourceImageUrl in Phase 1; 2D try-on available.",
                    customerId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Unexpected error during 3D body generation for CustomerId {CustomerId}. " +
                    "Continuing without 3D model (2D try-on still available).",
                    customerId);
            }

            // ── SECOND SaveChangesAsync (only when 3D succeeded) ──────────
            if (avatar3dModelUrl is not null)
            {
                avatar.SetAvatar3dModelUrl(avatar3dModelUrl, avatarFocalLength);

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Phase-2 save completed. CustomerId: {CustomerId}, 3D model persisted.",
                    customerId);
            }
        }

        // Invalidate the avatar cache so GetAvatar returns the fresh data immediately.
        await _cacheService.RemoveAsync($"avatar:{customerId:N}", cancellationToken);

        return avatar.ToDto();
    }

    // ── Magic byte constants ────────────────────────────────────────────────
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];

    /// <summary>
    /// Reads the first 4 bytes of the stream to verify the JPEG or PNG file signature.
    /// Throws <see cref="BusinessRuleException"/> if the magic bytes do not match.
    /// </summary>
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

    /// <summary>Reads the entire content of a stream into a byte array.</summary>
    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
