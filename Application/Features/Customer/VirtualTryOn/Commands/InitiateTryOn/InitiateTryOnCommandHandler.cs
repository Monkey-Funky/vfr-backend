using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
using Domain.Entities.Customer;
using Domain.Enums.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.VirtualTryOn.Commands.InitiateTryOn;

public sealed class InitiateTryOnCommandHandler : IRequestHandler<InitiateTryOnCommand, TryOnResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVirtualTryOnService _virtualTryOnService;
    private readonly IVirtualTryOn2DService _virtualTryOn2DService;
    private readonly ICacheService _cacheService;

    public InitiateTryOnCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IVirtualTryOnService virtualTryOnService,
        IVirtualTryOn2DService virtualTryOn2DService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _virtualTryOnService = virtualTryOnService;
        _virtualTryOn2DService = virtualTryOn2DService;
        _cacheService = cacheService;
    }

    public async Task<TryOnResultDto> Handle(InitiateTryOnCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("Customer identity missing.");

        // 1. Validate product exists and is active.
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null || product.Status != "Active")
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

        // 2b. For Overlay2D: validate avatar eligibility BEFORE creating the session.
        //
        // The avatar-missing and source-image-missing checks are pure business-rule
        // violations that can be detected from already-loaded data — there is no
        // reason to persist a session that is guaranteed to fail because of a
        // predictable client-side data gap.  Throwing here returns a clean 422 to
        // the caller without ever writing a "Failed" session record to the database.
        //
        // (For the 3D path the avatar check is also predictable, but the 3D service
        // owns that validation so that it can remain self-contained and testable.)
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

        // 3. Persist the pending session before calling the external service.
        //
        // At this point all predictable validation errors have already been surfaced
        // (step 2 / 2b). The only remaining failure modes are transient external
        // service errors — those are captured in the catch block below so the session
        // is recorded as Failed rather than left in a perpetual Pending state.
        var session = VirtualTryOnSession.Create(
            customerId: customerId,
            productId: request.ProductId,
            retailerId: product.RetailerId,
            sessionType: request.SessionType,
            avatarId: request.AvatarId);

        _context.VirtualTryOnSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        // 4. Call the external try-on service; mark the session failed on any exception.
        //    Overlay2D uses the flat 2D image pipeline; Model3D/ARLiveView keep the
        //    existing 3D SAM pipeline unchanged.
        TryOnResultDto result;
        try
        {
            result = request.SessionType == TryOnSessionType.Overlay2D
                // activeAvatar is guaranteed non-null here: step 2b would have thrown
                // for Overlay2D if AvatarId was absent or the avatar was not found.
                ? await ProcessOverlay2DAsync(request, customerId, activeAvatar!, cancellationToken)
                : await _virtualTryOnService.ProcessTryOnAsync(
                    customerId,
                    request.ProductId,
                    request.SessionType,
                    activeAvatar,
                    cancellationToken);
        }
        catch
        {
            session.MarkAsFailed();
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }

        // 5. Update session status based on service result.
        if (result.Status == SessionStatus.Failed)
        {
            session.MarkAsFailed();
        }
        else if (result.Status == SessionStatus.Completed && result.ResultImageUrl is not null)
        {
            session.MarkAsCompleted(
                result.ResultImageUrl,
                result.RecommendedSize,
                result.ConfidenceScore,
                result.DurationSeconds ?? 0);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate the paginated try-on session list for this customer so the new
        // session appears immediately on the next fetch (all pages, all products).
        await _cacheService.RemoveByPrefixAsync($"tryon:{customerId:N}:", cancellationToken);

        return result;
    }

    /// <summary>
    /// Runs the 2D (Overlay2D) try-on pipeline: sends the customer's stored front
    /// photo as the person image and the product's primary image as the garment image
    /// to the fal.ai FASHN model, receiving a composited 2D result image.
    ///
    /// <para>
    /// Unlike the 3D path this does NOT require a generated 3D avatar model — only
    /// an avatar record with a usable source image (<c>avatar.SourceImageUrl</c>).
    /// </para>
    ///
    /// <para><b>Pre-conditions enforced by the caller (step 2b):</b></para>
    /// <list type="bullet">
    ///   <item><paramref name="avatar"/> is non-null.</item>
    ///   <item><c>avatar.SourceImageUrl</c> is non-null / non-whitespace.</item>
    /// </list>
    /// </summary>
    private async Task<TryOnResultDto> ProcessOverlay2DAsync(
        InitiateTryOnCommand request,
        Guid customerId,
        Domain.Entities.Customer.Avatar avatar,       // guaranteed non-null by step 2b
        CancellationToken cancellationToken)
    {
        // Garment image: the product's primary image (first non-deleted by display order).
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
                PersonImageUrl: avatar.SourceImageUrl!,   // non-null: validated by step 2b
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