using System.Text.Json;
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
    private readonly IAiGenerationCacheService _aiCache;
    private readonly ILogger<ExtractMeasurementsFromImageCommandHandler> _logger;

    public ExtractMeasurementsFromImageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IBodyMeasurementExtractionService extractionService,
        IFalAiService falAiService,
        IFileStorageService fileStorageService,
        ICacheService cacheService,
        IAiGenerationCacheService aiCache,
        ILogger<ExtractMeasurementsFromImageCommandHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _extractionService = extractionService;
        _falAiService = falAiService;
        _fileStorageService = fileStorageService;
        _cacheService = cacheService;
        _aiCache = aiCache;
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

        // 2. Compute image hashes for cache key (before any AI calls).
        var frontImageHash = _aiCache.HashBytes(frontBytes);
        var sideImageHash = _aiCache.HashBytes(sideBytes);

        var avatarRequestHash = _aiCache.ComputeAvatarHash(
            frontImageHash: frontImageHash,
            sideImageHash: sideImageHash,
            heightCm: request.HeightCm,
            provider: "FalAi",
            bodyModelId: _aiCache.FalAiBodyModelId,
            measurementModelId: "BodyMeasurementExtraction",
            pipelineVersion: _aiCache.PipelineVersion);

        _logger.LogInformation(
            "Avatar generation request. CustomerId: {CustomerId}, Hash: {HashPrefix}...",
            customerId, avatarRequestHash[..8]);

        // 3. Check the AI generation deduplication cache before calling fal.ai.
        //    This prevents paying for the same generation twice.
        var existingCacheEntry = await _aiCache.GetByHashAsync(avatarRequestHash, cancellationToken);

        if (existingCacheEntry is not null)
        {
            if (existingCacheEntry.Status == AiGenerationStatus.Processing)
            {
                _logger.LogInformation(
                    "Avatar generation already in progress for hash {HashPrefix}...", avatarRequestHash[..8]);
                throw new BusinessRuleException(
                    "AI_GENERATION_IN_PROGRESS",
                    "The same avatar generation is already in progress. Please wait and try again shortly.");
            }

            if (existingCacheEntry.Status == AiGenerationStatus.Completed)
            {
                _logger.LogInformation(
                    "Cache hit for avatar generation. Hash: {HashPrefix}..., Reusing result.", avatarRequestHash[..8]);

                return await ApplyCachedAvatarResultAsync(
                    customerId, existingCacheEntry, request.HeightCm, cancellationToken);
            }

            if (existingCacheEntry.Status == AiGenerationStatus.Failed)
            {
                var retryWindowExpired = existingCacheEntry.FailedAt.HasValue &&
                    existingCacheEntry.FailedAt.Value < DateTime.UtcNow.AddHours(-_aiCache.FailedRetryWindowHours);

                if (!retryWindowExpired)
                {
                    _logger.LogInformation(
                        "Previous avatar generation failed for hash {HashPrefix}... and retry window has not expired.",
                        avatarRequestHash[..8]);
                    throw new BusinessRuleException(
                        "AI_GENERATION_PREVIOUSLY_FAILED",
                        "A previous attempt to generate this avatar failed. Please try again later.");
                }

                _logger.LogInformation(
                    "Retrying failed avatar generation for hash {HashPrefix}...", avatarRequestHash[..8]);
            }
        }

        // 4. Check daily paid generation quota.
        if (await _aiCache.IsAvatarQuotaExceededAsync(customerId, cancellationToken))
        {
            throw new BusinessRuleException(
                "AI_GENERATION_QUOTA_EXCEEDED",
                "Daily AI generation limit reached. Please try again tomorrow. Cached results remain available.");
        }

        // 5. Send both images to the AI model (uses throwaway streams from buffered bytes).
        var measurements = await _extractionService.ExtractAsync(
            new MemoryStream(frontBytes, writable: false),
            request.FrontImageFile.FileName,
            request.FrontImageFile.ContentType,
            new MemoryStream(sideBytes, writable: false),
            request.SideImageFile.FileName,
            request.SideImageFile.ContentType,
            request.HeightCm,
            cancellationToken);

        // 6. Upload the front image to get a stable public URL.
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
                avatar3dModelUrl: null,
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
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Phase-1 save completed. CustomerId: {CustomerId}, AvatarId: {AvatarId}, " +
            "SourceImageUrl saved: {HasSource}",
            customerId, avatar.Id, sourceImageUrl is not null);

        // ══════════════════════════════════════════════════════════════════
        // PHASE 2 — 3D body-model generation (best-effort, independent).
        // ══════════════════════════════════════════════════════════════════
        if (sourceImageUrl is not null)
        {
            // Insert cache row as Processing BEFORE calling fal.ai.
            var inputJson = JsonSerializer.Serialize(new
            {
                frontImageHash,
                sideImageHash,
                heightCm = request.HeightCm,
                provider = "FalAi",
                bodyModelId = _aiCache.FalAiBodyModelId,
                pipelineVersion = _aiCache.PipelineVersion
            });

            AiGenerationCache? cacheEntry = null;
            try
            {
                cacheEntry = await _aiCache.TryCreateProcessingAsync(
                    customerId: customerId,
                    requestHash: avatarRequestHash,
                    type: AiGenerationType.Avatar3D,
                    provider: "FalAi",
                    modelId: _aiCache.FalAiBodyModelId,
                    pipelineVersion: _aiCache.PipelineVersion,
                    inputJson: inputJson,
                    ct: cancellationToken);

                // If null: race condition — another request inserted the same hash.
                cacheEntry ??= await _aiCache.GetByHashAsync(avatarRequestHash, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Failed to create AI generation cache entry for CustomerId {CustomerId}. Proceeding without cache.",
                    customerId);
            }

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

                // Update cache to Completed.
                if (cacheEntry is not null)
                {
                    var resultJson = JsonSerializer.Serialize(new
                    {
                        glbUrl = avatar3dModelUrl,
                        focalLength = avatarFocalLength,
                        sourceImageUrl
                    });
                    await _aiCache.MarkCompletedAsync(cacheEntry.Id, resultImageUrl: sourceImageUrl, resultModelUrl: avatar3dModelUrl, resultJson, cancellationToken);
                }
            }
            catch (ExternalServiceException ex)
            {
                _logger.LogWarning(ex,
                    "fal.ai 3D body generation failed for CustomerId {CustomerId}. " +
                    "2D try-on remains available via the SourceImageUrl persisted in Phase 1.",
                    customerId);

                if (cacheEntry is not null)
                    await SafeMarkFailedAsync(cacheEntry.Id, "ExternalServiceError", ex.Message, cancellationToken);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken != cancellationToken)
            {
                _logger.LogWarning(ex,
                    "fal.ai 3D body generation timed out (Polly internal CT) for CustomerId {CustomerId}. " +
                    "Avatar already saved with SourceImageUrl in Phase 1; 2D try-on available.",
                    customerId);

                if (cacheEntry is not null)
                    await SafeMarkFailedAsync(cacheEntry.Id, "Timeout", "fal.ai 3D body generation timed out.", cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Unexpected error during 3D body generation for CustomerId {CustomerId}. " +
                    "Continuing without 3D model (2D try-on still available).",
                    customerId);

                if (cacheEntry is not null)
                    await SafeMarkFailedAsync(cacheEntry.Id, "UnexpectedError", "Unexpected error during 3D body generation.", cancellationToken);
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

    // ── Apply cached 3D result ────────────────────────────────────────────────

    private async Task<AvatarDto> ApplyCachedAvatarResultAsync(
        Guid customerId,
        AiGenerationCache cached,
        decimal heightCm,
        CancellationToken ct)
    {
        // Deserialize the cached result to get 3D model URLs + focal length.
        string? cachedGlbUrl = cached.ResultModelUrl;
        string? cachedSourceImageUrl = cached.ResultImageUrl;
        double? cachedFocalLength = null;

        if (cached.ResultJson is not null)
        {
            try
            {
                var resultDoc = JsonSerializer.Deserialize<JsonElement>(cached.ResultJson);
                if (resultDoc.TryGetProperty("focalLength", out var fl) && fl.ValueKind != JsonValueKind.Null)
                    cachedFocalLength = fl.GetDouble();
            }
            catch
            {
                // non-critical — fallback to null
            }
        }

        // Upsert avatar using the cached 3D result.
        var avatar = await _context.Avatars
            .FirstOrDefaultAsync(a => a.CustomerId == customerId, ct);

        if (avatar is null)
        {
            // We can't restore full measurements from the cache (we don't store them there),
            // but we can at least restore the source image and 3D model.
            _logger.LogWarning(
                "Cache hit for avatar but no existing avatar record for CustomerId {CustomerId}. " +
                "Cannot fully restore cached result. Returning cache hit without avatar creation.",
                customerId);

            // In this edge case, fall through to a normal generation.
            // This is rare: it would only happen if the cache entry exists but the avatar was deleted.
            throw new BusinessRuleException(
                "AI_GENERATION_CACHE_INCONSISTENCY",
                "A cached avatar generation result was found but your avatar record no longer exists. Please try again.");
        }

        if (cachedSourceImageUrl is not null)
            avatar.SetSourceImageUrl(cachedSourceImageUrl);

        if (cachedGlbUrl is not null)
            avatar.SetAvatar3dModelUrl(cachedGlbUrl, cachedFocalLength);

        await _context.SaveChangesAsync(ct);
        await _cacheService.RemoveAsync($"avatar:{customerId:N}", ct);

        _logger.LogInformation(
            "Cached avatar 3D result applied for CustomerId {CustomerId}. GlbUrl: {GlbUrl}",
            customerId, cachedGlbUrl);

        return avatar.ToDto();
    }

    private async Task SafeMarkFailedAsync(Guid id, string errorCode, string message, CancellationToken ct)
    {
        try
        {
            await _aiCache.MarkFailedAsync(id, errorCode, message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to mark AI generation cache entry {Id} as failed.", id);
        }
    }

    // ── Magic byte constants ────────────────────────────────────────────────
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
