using Application.Mappings;
using Domain.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Subscriptions.Queries.GetCurrentSubscriptionDetails;

public sealed class GetCurrentSubscriptionDetailsQueryHandler
    : IRequestHandler<GetCurrentSubscriptionDetailsQuery, CurrentSubscriptionDetailsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetCurrentSubscriptionDetailsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<CurrentSubscriptionDetailsDto> Handle(
        GetCurrentSubscriptionDetailsQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        Subscription subscription = await _context.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Include(s => s.PendingDowngradePlan)
            .FirstOrDefaultAsync(s => s.RetailerId == retailerId, cancellationToken)
            ?? throw new NotFoundException(
                "No subscription found for this retailer.");

        SubscriptionPlan plan = subscription.Plan;

        return new CurrentSubscriptionDetailsDto(
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
            CommissionRate: plan.CommissionRate,
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
    }
}