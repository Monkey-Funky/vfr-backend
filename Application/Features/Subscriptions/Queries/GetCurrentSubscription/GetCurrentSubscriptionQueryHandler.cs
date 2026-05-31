using Application.Features.Subscriptions.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Subscriptions.Queries.GetCurrentSubscription;

/// <summary>
/// Returns the current subscription for the authenticated retailer.
/// Cache-aside: TTL 5 minutes. Invalidated by all subscription command handlers.
/// </summary>
public sealed class GetCurrentSubscriptionQueryHandler
    : IRequestHandler<GetCurrentSubscriptionQuery, CurrentSubscriptionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetCurrentSubscriptionQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<CurrentSubscriptionDto> Handle(
        GetCurrentSubscriptionQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey = CacheKeys.CurrentSubscription(retailerId);

        var cached = await _cacheService.GetAsync<CurrentSubscriptionDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        Subscription subscription = await _context.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Include(s => s.PendingDowngradePlan)
            .FirstOrDefaultAsync(s => s.RetailerId == retailerId, cancellationToken)
            ?? throw new NotFoundException(
                "No subscription found for this retailer. Please start a trial or select a plan.");

        bool canUpgrade = subscription.Status is SubscriptionStatus.Active
                                                  or SubscriptionStatus.PendingDowngrade
                                                  or SubscriptionStatus.Trial;
        bool canDowngrade = subscription.Status is SubscriptionStatus.Active;
        bool canCancel = subscription.Status is not (SubscriptionStatus.Cancelled
                                                       or SubscriptionStatus.Expired
                                                       or SubscriptionStatus.None);
        bool canStartTrial = subscription.Status == SubscriptionStatus.None;

        var dto = new CurrentSubscriptionDto(
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

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);

        return dto;
    }
}
