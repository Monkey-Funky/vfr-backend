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

        BodyMeasurements measurements;
        string? avatar3dModelUrl = null;

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
        measurements = await _extractionService.ExtractAsync(
            new MemoryStream(frontBytes, writable: false),
            request.FrontImageFile.FileName,
            request.FrontImageFile.ContentType,
            new MemoryStream(sideBytes, writable: false),
            request.SideImageFile.FileName,
            request.SideImageFile.ContentType,
            request.HeightCm,
            cancellationToken);

        // 3. Generate 3D avatar model using the full preprocessing pipeline (best-effort).
        //    Pipeline: Upload both images → BiRefNet (bg removal) → AuraSR (upscale) → Rodin (3D)
        //    Using BOTH front+side images gives Rodin multi-view data for much better 3D reconstruction.
        try
        {
            // 3a. Upload both images to Cloudinary for public URLs (parallel).
            var frontFileName = $"{customerId}_{Guid.NewGuid():N}_front{Path.GetExtension(request.FrontImageFile.FileName)}";
            var sideFileName = $"{customerId}_{Guid.NewGuid():N}_side{Path.GetExtension(request.SideImageFile.FileName)}";

            var frontUploadTask = _fileStorageService.UploadAsync(
                new MemoryStream(frontBytes, writable: false), frontFileName, "avatars/3d-source", cancellationToken);
            var sideUploadTask = _fileStorageService.UploadAsync(
                new MemoryStream(sideBytes, writable: false), sideFileName, "avatars/3d-source", cancellationToken);

            var cloudinaryUrls = await Task.WhenAll(frontUploadTask, sideUploadTask);
            var frontCloudinaryUrl = cloudinaryUrls[0];
            var sideCloudinaryUrl = cloudinaryUrls[1];

            _logger.LogInformation(
                "Uploaded both source images for 3D generation. CustomerId: {CustomerId}, Front: {FrontUrl}, Side: {SideUrl}",
                customerId, frontCloudinaryUrl, sideCloudinaryUrl);

            // 3b. Preprocess: Background removal + Upscale (parallel for both images).
            var frontBgTask = _falAiService.RemoveBackgroundAsync(frontCloudinaryUrl, cancellationToken);
            var sideBgTask = _falAiService.RemoveBackgroundAsync(sideCloudinaryUrl, cancellationToken);

            var bgRemovedUrls = await Task.WhenAll(frontBgTask, sideBgTask);

            _logger.LogInformation(
                "Background removed from both images. CustomerId: {CustomerId}",
                customerId);

            var frontUpscaleTask = _falAiService.UpscaleImageAsync(bgRemovedUrls[0], cancellationToken);
            var sideUpscaleTask = _falAiService.UpscaleImageAsync(bgRemovedUrls[1], cancellationToken);

            var upscaledUrls = await Task.WhenAll(frontUpscaleTask, sideUpscaleTask);

            _logger.LogInformation(
                "Both images upscaled. CustomerId: {CustomerId}",
                customerId);

            // 3c. Generate 3D avatar from BOTH preprocessed images (multi-view concat mode).
            var avatarResult = await _falAiService.GenerateAvatar3dAsync(
                upscaledUrls, cancellationToken);
            avatar3dModelUrl = avatarResult.GlbUrl;

            _logger.LogInformation(
                "3D avatar model generated successfully. CustomerId: {CustomerId}, GlbUrl: {GlbUrl}",
                customerId, avatar3dModelUrl);
        }
        catch (ExternalServiceException ex)
        {
            _logger.LogWarning(ex,
                "fal.ai 3D avatar generation pipeline failed for CustomerId {CustomerId}. Continuing without 3D model.",
                customerId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Unexpected error during 3D avatar generation pipeline for CustomerId {CustomerId}. Continuing without 3D model.",
                customerId);
        }

        const string source = "AIEstimate";

        // 4. Upsert avatar.
        var existingAvatar = await _context.Avatars
            .FirstOrDefaultAsync(a => a.CustomerId == customerId, cancellationToken);

        Domain.Entities.Customer.Avatar avatar;

        if (existingAvatar is not null)
        {
            existingAvatar.UpdateMeasurements(measurements, source);
            if (avatar3dModelUrl is not null)
                existingAvatar.SetAvatar3dModelUrl(avatar3dModelUrl);
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
                avatar3dModelUrl: avatar3dModelUrl);
            _context.Avatars.Add(avatar);
        }

        // 5. Record history snapshot atomically.
        var measurementsJson = Domain.Entities.Customer.Avatar.BuildMeasurementJson(measurements);
        var history = AvatarMeasurementHistory.CreateSnapshot(
            avatarId: avatar.Id,
            measurementDataJson: measurementsJson,
            source: source);
        _context.AvatarMeasurementHistory.Add(history);

        await _context.SaveChangesAsync(cancellationToken);

        return avatar.ToDto();
    }

    // ── Magic byte constants ────────────────────────────────────────────────
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];

    /// <summary>
    /// Reads the first 4 bytes of the stream to verify the JPEG or PNG file signature.
    /// Throws <see cref="BusinessRuleException"/> if the magic bytes do not match.
    /// The caller is responsible for resetting the stream position after this method returns.
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

    /// <summary>
    /// Reads the entire content of a stream into a byte array.
    /// </summary>
    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
