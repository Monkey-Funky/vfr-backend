using Application.Features.Subscriptions.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Subscriptions.Queries.GetCurrentSubscriptionDetails;

/// <summary>
/// Returns detailed subscription + plan info for the authenticated retailer.
/// Cache-aside: TTL 5 minutes. Invalidated by all subscription mutation commands.
/// </summary>
public sealed class GetCurrentSubscriptionDetailsQueryHandler
    : IRequestHandler<GetCurrentSubscriptionDetailsQuery, CurrentSubscriptionDetailsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetCurrentSubscriptionDetailsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<CurrentSubscriptionDetailsDto> Handle(
        GetCurrentSubscriptionDetailsQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey = $"sub_details:{retailerId:N}";

        var cached = await _cacheService.GetAsync<CurrentSubscriptionDetailsDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        Subscription subscription = await _context.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Include(s => s.PendingDowngradePlan)
            .FirstOrDefaultAsync(s => s.RetailerId == retailerId, cancellationToken)
            ?? throw new NotFoundException("No subscription found for this retailer.");

        SubscriptionPlan plan = subscription.Plan;

        var dto = new CurrentSubscriptionDetailsDto(
            SubscriptionId: subscription.Id,
            Status: subscription.Status,
            StartDate: subscription.StartDate,
            EndDate: subscription.EndDate,
            TrialEndsAt: subscription.TrialEndsAt,
            IsRecurringEnabled: subscription.IsRecurringEnabled,
            PlanId: plan.Id,
            PlanName: plan.Name,
            Tier: plan.Tier,
            BillingCycle: plan.BillingCycle,
            PriceAmount: plan.PriceAmount,
            Currency: plan.Currency,
            MaxActiveProducts: plan.MaxActiveProducts,
            MaxMonthlyTryOns: plan.MaxMonthlyTryOns,
            SupportLevel: plan.SupportLevel,
            IsUnlimited: plan.IsUnlimited,
            IsWhiteLabel: plan.IsWhiteLabel,
            IncludesSourceCode: plan.IncludesSourceCode,
            IncludesMobileApps: plan.IncludesMobileApps,
            HasSla: plan.HasSla,
            HasDedicatedTeam: plan.HasDedicatedTeam,
            PendingDowngradePlanId: subscription.PendingDowngradePlanId,
            PendingDowngradePlanName: subscription.PendingDowngradePlan?.Name,
            PendingDowngradeEffectiveAt: subscription.PendingDowngradeEffectiveAt
        );

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);

        return dto;
    }
}
