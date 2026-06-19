using System.Text.Json;
using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
using Domain.Entities.Customer;
using Domain.Enums.Customer;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.VirtualTryOn.Commands.InitiateTryOn;

public sealed class InitiateTryOnCommandHandler : IRequestHandler<InitiateTryOnCommand, TryOnResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVirtualTryOnService _virtualTryOnService;
    private readonly IVirtualTryOn2DService _virtualTryOn2DService;
    private readonly ICacheService _cacheService;
    private readonly IAiGenerationCacheService _aiCache;

    public InitiateTryOnCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IVirtualTryOnService virtualTryOnService,
        IVirtualTryOn2DService virtualTryOn2DService,
        ICacheService cacheService,
        IAiGenerationCacheService aiCache)
    {
        _context = context;
        _currentUserService = currentUserService;
        _virtualTryOnService = virtualTryOnService;
        _virtualTryOn2DService = virtualTryOn2DService;
        _cacheService = cacheService;
        _aiCache = aiCache;
    }

    public async Task<TryOnResultDto> Handle(InitiateTryOnCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("Customer identity missing.");

        // 1. Validate product exists and is active.
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null || product.Status != ProductStatus.Active)
            throw new NotFoundException("Product", request.ProductId);

        // 2. Load and validate avatar ownership if one is provided.
        Domain.Entities.Customer.Avatar? activeAvatar = null;
        if (request.AvatarId.HasValue)
        {
            activeAvatar = await _context.Avatars
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.AvatarId.Value, cancellationToken);

            if (activeAvatar is null || activeAvatar.CustomerId != customerId)
                throw new NotFoundException("Avatar", request.AvatarId.Value);
        }

        // 2b. Pre-persist business-rule validation for all session types.
        if (request.SessionType == TryOnSessionType.Overlay2D)
        {
            if (!request.AvatarId.HasValue || activeAvatar is null)
                throw new BusinessRuleException(
                    "TryOn2DAvatarRequired",
                    "2D try-on requires an avatar. Please create one first.");

            if (string.IsNullOrWhiteSpace(activeAvatar.SourceImageUrl))
                throw new BusinessRuleException(
                    "TryOn2DSourceImageMissing",
                    "This avatar has no source image. Please re-create the avatar from a photo.");
        }
        else if (request.SessionType == TryOnSessionType.Model3D
              || request.SessionType == TryOnSessionType.ARLiveView)
        {
            if (!request.AvatarId.HasValue || activeAvatar is null)
                throw new BusinessRuleException(
                    "TryOn3DAvatarRequired",
                    "3D try-on requires a 3D avatar. Please create one first.");

            if (string.IsNullOrWhiteSpace(activeAvatar.Avatar3dModelUrl))
                throw new BusinessRuleException(
                    "TryOn3DAvatarRequired",
                    "Customer does not have a 3D avatar model. Please re-create the avatar from a photo.");

            if (string.IsNullOrWhiteSpace(activeAvatar.SourceImageUrl))
                throw new BusinessRuleException(
                    "TryOn3DSourceImageMissing",
                    "This avatar has no source image. Please re-create the avatar from a photo.");
        }

        // 3. Load product image (needed for cache key and for the AI call).
        var productImageRow = await _context.ProductImages
            .Where(i => i.ProductId == request.ProductId && !i.IsDeleted)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new { i.ImageUrl })
            .FirstOrDefaultAsync(cancellationToken);

        var productImageUrl = productImageRow?.ImageUrl;
        var productImageUpdatedAt = product.UpdatedAt;

        // 4. AI generation deduplication cache check.
        if (activeAvatar is not null && productImageUrl is not null)
        {
            var (cacheType, requestHash) = ComputeTryOnHash(request.SessionType, activeAvatar, request.ProductId, productImageUrl, productImageUpdatedAt);

            var cached = await _aiCache.GetByHashAsync(requestHash, cancellationToken);

            if (cached is not null)
            {
                if (cached.Status == AiGenerationStatus.Processing)
                {
                    throw new BusinessRuleException(
                        "AI_GENERATION_IN_PROGRESS",
                        "The same try-on generation is already in progress. Please wait and try again shortly.");
                }

                if (cached.Status == AiGenerationStatus.Completed &&
                    (cached.ResultImageUrl is not null || cached.ResultModelUrl is not null))
                {
                    return await BuildCachedTryOnResultAsync(
                        customerId, request, product, activeAvatar, cached, cancellationToken);
                }

                if (cached.Status == AiGenerationStatus.Failed)
                {
                    var retryWindowExpired = cached.FailedAt.HasValue &&
                        cached.FailedAt.Value < DateTime.UtcNow.AddHours(-_aiCache.FailedRetryWindowHours);

                    if (!retryWindowExpired)
                    {
                        throw new BusinessRuleException(
                            "AI_GENERATION_PREVIOUSLY_FAILED",
                            "A previous attempt at this try-on failed. Please try again later.");
                    }
                }
            }
        }

        // 5. Check daily paid quota.
        if (await _aiCache.IsTryOnQuotaExceededAsync(customerId, cancellationToken))
        {
            throw new BusinessRuleException(
                "AI_GENERATION_QUOTA_EXCEEDED",
                "Daily AI generation limit reached. Cached results remain available.");
        }

        // 6. Persist the pending session before calling the external service.
        var session = VirtualTryOnSession.Create(
            customerId: customerId,
            productId: request.ProductId,
            retailerId: product.RetailerId,
            sessionType: request.SessionType,
            avatarId: request.AvatarId);

        _context.VirtualTryOnSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        // 7. Create cache entry (Processing) if we have all the inputs.
        AiGenerationCache? cacheEntry = null;
        if (activeAvatar is not null && productImageUrl is not null)
        {
            var (cacheType, requestHash) = ComputeTryOnHash(request.SessionType, activeAvatar, request.ProductId, productImageUrl, productImageUpdatedAt);
            var inputJson = JsonSerializer.Serialize(new
            {
                type = cacheType,
                avatarId = activeAvatar.Id,
                productId = request.ProductId,
                productImageUrl,
                pipelineVersion = _aiCache.PipelineVersion
            });

            try
            {
                cacheEntry = await _aiCache.TryCreateProcessingAsync(
                    customerId: customerId,
                    requestHash: requestHash,
                    type: cacheType,
                    provider: "FalAi",
                    modelId: cacheType == AiGenerationType.TryOn2D ? _aiCache.TryOn2DModelId : _aiCache.FalAiObjectsModelId,
                    pipelineVersion: _aiCache.PipelineVersion,
                    inputJson: inputJson,
                    ct: cancellationToken);

                cacheEntry ??= await _aiCache.GetByHashAsync(requestHash, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Non-fatal: proceed without cache tracking
                _ = ex;
            }
        }

        // 8. Call the external try-on service; mark the session failed on any exception.
        TryOnResultDto result;
        try
        {
            result = request.SessionType == TryOnSessionType.Overlay2D
                ? await ProcessOverlay2DAsync(request, customerId, activeAvatar!, cancellationToken)
                : await _virtualTryOnService.ProcessTryOnAsync(
                    customerId,
                    request.ProductId,
                    request.SessionType,
                    activeAvatar,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            session.MarkAsFailed();
            await _context.SaveChangesAsync(cancellationToken);

            if (cacheEntry is not null)
            {
                try { await _aiCache.MarkFailedAsync(cacheEntry.Id, ex.GetType().Name, ex.Message, cancellationToken); }
                catch { /* non-fatal */ }
            }

            throw;
        }

        // 9. Update session status.
        if (result.Status == SessionStatus.Failed)
        {
            session.MarkAsFailed();
            if (cacheEntry is not null)
            {
                try { await _aiCache.MarkFailedAsync(cacheEntry.Id, "ServiceFailed", "Try-on service returned Failed status.", cancellationToken); }
                catch { /* non-fatal */ }
            }
        }
        else if (result.Status == SessionStatus.Completed && result.ResultImageUrl is not null)
        {
            session.MarkAsCompleted(
                result.ResultImageUrl,
                result.RecommendedSize,
                result.ConfidenceScore,
                result.DurationSeconds ?? 0);

            // Update cache to Completed.
            if (cacheEntry is not null)
            {
                var (cacheType, _) = ComputeTryOnHash(request.SessionType, activeAvatar!, request.ProductId, productImageUrl ?? "", productImageUpdatedAt);
                var resultImageUrl = cacheType == AiGenerationType.TryOn2D ? result.ResultImageUrl : null;
                var resultModelUrl = cacheType == AiGenerationType.TryOn3D ? result.ResultImageUrl : null;
                try { await _aiCache.MarkCompletedAsync(cacheEntry.Id, resultImageUrl, resultModelUrl, null, cancellationToken); }
                catch { /* non-fatal */ }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate the paginated try-on session list for this customer.
        await _cacheService.RemoveByPrefixAsync($"tryon:{customerId:N}:", cancellationToken);

        return result;
    }

    // ── Cache helpers ─────────────────────────────────────────────────────────

    private (string cacheType, string requestHash) ComputeTryOnHash(
        TryOnSessionType sessionType,
        Domain.Entities.Customer.Avatar avatar,
        Guid productId,
        string productImageUrl,
        DateTime? productImageUpdatedAt)
    {
        if (sessionType == TryOnSessionType.Overlay2D)
        {
            var hash = _aiCache.ComputeTryOn2DHash(
                avatarId: avatar.Id,
                avatarFrontImageUrl: avatar.SourceImageUrl ?? "",
                productId: productId,
                productImageUrl: productImageUrl,
                productImageUpdatedAt: productImageUpdatedAt,
                selectedSize: null,
                selectedColor: null,
                provider: "FalAi",
                tryOn2DModelId: _aiCache.TryOn2DModelId,
                pipelineVersion: _aiCache.PipelineVersion);
            return (AiGenerationType.TryOn2D, hash);
        }
        else
        {
            var hash = _aiCache.ComputeTryOn3DHash(
                avatarId: avatar.Id,
                avatar3dModelUrl: avatar.Avatar3dModelUrl ?? "",
                avatarFocalLength: avatar.AvatarFocalLength ?? 1000.0,
                sourceImageUrl: avatar.SourceImageUrl ?? "",
                productId: productId,
                productImageUrl: productImageUrl,
                productImageUpdatedAt: productImageUpdatedAt,
                selectedSize: null,
                selectedColor: null,
                provider: "FalAi",
                objectsModelId: _aiCache.FalAiObjectsModelId,
                alignModelId: _aiCache.FalAiAlignModelId,
                pipelineVersion: _aiCache.PipelineVersion);
            return (AiGenerationType.TryOn3D, hash);
        }
    }

    private async Task<TryOnResultDto> BuildCachedTryOnResultAsync(
        Guid customerId,
        InitiateTryOnCommand request,
        Domain.Entities.Retailer.Product product,
        Domain.Entities.Customer.Avatar activeAvatar,
        AiGenerationCache cached,
        CancellationToken ct)
    {
        // Still create a VirtualTryOnSession record so the user's history is complete.
        var cachedResultUrl = cached.ResultImageUrl ?? cached.ResultModelUrl!;

        var session = VirtualTryOnSession.Create(
            customerId: customerId,
            productId: request.ProductId,
            retailerId: product.RetailerId,
            sessionType: request.SessionType,
            avatarId: request.AvatarId);

        _context.VirtualTryOnSessions.Add(session);
        session.MarkAsCompleted(cachedResultUrl, recommendedSize: null, confidenceScore: 0.98m, durationSeconds: 0);
        await _context.SaveChangesAsync(ct);
        await _cacheService.RemoveByPrefixAsync($"tryon:{customerId:N}:", ct);

        var resultType = request.SessionType == TryOnSessionType.Overlay2D
            ? TryOnResultType.Image2D
            : TryOnResultType.Model3D;

        return new TryOnResultDto(
            Status: SessionStatus.Completed,
            ResultImageUrl: cachedResultUrl,
            RecommendedSize: null,
            ConfidenceScore: 0.98m,
            DurationSeconds: 0,
            ResultType: resultType);
    }

    // ── 2D try-on pipeline ────────────────────────────────────────────────────

    private async Task<TryOnResultDto> ProcessOverlay2DAsync(
        InitiateTryOnCommand request,
        Guid customerId,
        Domain.Entities.Customer.Avatar avatar,
        CancellationToken cancellationToken)
    {
        var garmentImageUrl = await _context.Products
            .Where(p => p.Id == request.ProductId)
            .SelectMany(p => p.Images)
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => i.ImageUrl)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(garmentImageUrl))
            throw new BusinessRuleException(
                "TryOn2DProductImageMissing",
                "This product has no image available for try-on.");

        var tryOnResult = await _virtualTryOn2DService.ProcessTryOnAsync(
            new TryOn2DRequest(
                CustomerId: customerId,
                ProductId: request.ProductId,
                AvatarId: avatar.Id,
                PersonImageUrl: avatar.SourceImageUrl!,
                GarmentImageUrl: garmentImageUrl,
                Category: null,
                SelectedSize: null,
                SelectedColor: null),
            cancellationToken);

        return new TryOnResultDto(
            Status: SessionStatus.Completed,
            ResultImageUrl: tryOnResult.ResultImageUrl,
            RecommendedSize: null,
            ConfidenceScore: tryOnResult.ConfidenceScore,
            DurationSeconds: tryOnResult.DurationSeconds,
            ResultType: TryOnResultType.Image2D);
    }
}
