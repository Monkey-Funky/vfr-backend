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

        // 1. Open and validate BOTH streams, then send to AI.
        await using var frontStream = request.FrontImageFile.Content;
        await using var sideStream = request.SideImageFile.Content;

        // Validate magic bytes for both images before forwarding to AI.
        await ValidateImageMagicBytesAsync(frontStream, "front image", cancellationToken);
        if (frontStream.CanSeek) frontStream.Seek(0, SeekOrigin.Begin);

        await ValidateImageMagicBytesAsync(sideStream, "side image", cancellationToken);
        if (sideStream.CanSeek) sideStream.Seek(0, SeekOrigin.Begin);

        // 2. Send both images to the AI model.
        measurements = await _extractionService.ExtractAsync(
            frontStream,
            request.FrontImageFile.FileName,
            request.FrontImageFile.ContentType,
            sideStream,
            request.SideImageFile.FileName,
            request.SideImageFile.ContentType,
            request.HeightCm,
            cancellationToken);

        // 3. Generate 3D body model from the front image (best-effort — does not abort the flow).
        try
        {
            if (frontStream.CanSeek) frontStream.Seek(0, SeekOrigin.Begin);

            var uniqueFileName = $"{customerId}_{Guid.NewGuid():N}{Path.GetExtension(request.FrontImageFile.FileName)}";
            var cloudinaryUrl = await _fileStorageService.UploadAsync(
                frontStream, uniqueFileName, "avatars/3d-source", cancellationToken);

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
}
