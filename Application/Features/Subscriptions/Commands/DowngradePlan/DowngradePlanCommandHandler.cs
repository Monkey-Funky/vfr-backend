using Shared.Constants;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;

namespace Application.Features.Subscriptions.Commands.DowngradePlan;

public sealed class DowngradePlanCommandHandler
    : IRequestHandler<DowngradePlanCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public DowngradePlanCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<bool>> Handle(
        DowngradePlanCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // ── Load current subscription ─────────────────────────────────────────
        Subscription subscription = await _unitOfWork
            .Repository<Subscription>()
            .FirstOrDefaultAsync(s => s.RetailerId == retailerId, cancellationToken)
            ?? throw new NotFoundException(
                "No subscription found. Please select a plan first.");

        if (subscription.Status != SubscriptionStatus.Active)
            throw new BusinessRuleException(
                "DOWNGRADE_NOT_ALLOWED",
                $"Downgrades are only available for Active subscriptions. " +
                $"Current status: {subscription.Status}.");

        // ── Load and validate the target plan ────────────────────────────────
        SubscriptionPlan newPlan = await _unitOfWork
            .Repository<SubscriptionPlan>()
            .FirstOrDefaultAsync(
                p => p.Id == command.NewPlanId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), command.NewPlanId);

        SubscriptionPlan currentPlan = await _unitOfWork
            .Repository<SubscriptionPlan>()
            .FirstOrDefaultAsync(p => p.Id == subscription.PlanId, cancellationToken)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), subscription.PlanId);

        // Enforce that this is actually a downgrade (lower price).
        if (newPlan.PriceAmount >= currentPlan.PriceAmount)
            throw new BusinessRuleException(
                "DOWNGRADE_TO_HIGHER_PLAN",
                "The requested plan has a higher or equal price. " +
                "Use UpgradePlan to switch to a higher-tier plan.");

        // ── Schedule downgrade via domain method ──────────────────────────────
        DateTime effectiveAt = subscription.EndDate
            ?? throw new BusinessRuleException(
                "SUBSCRIPTION_NO_END_DATE",
                "Cannot schedule a downgrade for a subscription with no end date.");

        subscription.SetPendingDowngrade(newPlan.Id, effectiveAt);

        await _unitOfWork.Repository<Subscription>().UpdateAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── Invalidate caches ─────────────────────────────────────────────────
        await Task.WhenAll(
            _cacheService.RemoveByPrefixAsync($"subscriptions:{retailerId}:", cancellationToken),
            _cacheService.RemoveAsync(CacheKeys.CurrentSubscription(retailerId), cancellationToken),
            _cacheService.RemoveAsync($"sub_details:{retailerId:N}", cancellationToken)
        );

        return Result<bool>.Success(
            true,
            $"Downgrade to '{newPlan.Name}' has been scheduled and will take effect " +
            $"on {effectiveAt:MMMM dd, yyyy}. No charge applies now.");
    }
}