using Application.Features.Subscriptions.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Subscriptions.Queries.GetCurrentSubscription;

public sealed class GetCurrentSubscriptionQueryHandler
    : IRequestHandler<GetCurrentSubscriptionQuery, CurrentSubscriptionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetCurrentSubscriptionQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<CurrentSubscriptionDto> Handle(
        GetCurrentSubscriptionQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // Fetch current subscription with plan and pending-downgrade plan (if any).
        Subscription subscription = await _context.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Include(s => s.PendingDowngradePlan)
            .FirstOrDefaultAsync(s => s.RetailerId == retailerId, cancellationToken)
            ?? throw new NotFoundException(
                "No subscription found for this retailer. Please start a trial or select a plan.");

        // Derive UI button state from domain state.
        bool canUpgrade = subscription.Status is SubscriptionStatus.Active
                                                  or SubscriptionStatus.PendingDowngrade
                                                  or SubscriptionStatus.Trial;
        bool canDowngrade = subscription.Status is SubscriptionStatus.Active;
        bool canCancel = subscription.Status is not (SubscriptionStatus.Cancelled
                                                       or SubscriptionStatus.Expired
                                                       or SubscriptionStatus.None);
        bool canStartTrial = subscription.Status == SubscriptionStatus.None;

        return new CurrentSubscriptionDto(
            SubscriptionId: subscription.Id,
            Status: subscription.Status,
            StartDate: subscription.StartDate,
            EndDate: subscription.EndDate,
            TrialEndsAt: subscription.TrialEndsAt,
            IsRecurringEnabled: subscription.IsRecurringEnabled,
            IsActive: subscription.IsActive,
            IsInTrial: subscription.IsInTrial,
            CurrentPlan: subscription.Plan.ToDto(),
            PendingDowngradePlan: subscription.PendingDowngradePlan?.ToDto(),
            CanUpgrade: canUpgrade,
            CanDowngrade: canDowngrade,
            CanCancel: canCancel,
            CanStartTrial: canStartTrial
        );
    }
}