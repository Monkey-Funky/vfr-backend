using Domain.Enums;

namespace Infrastructure.Services;


/// <summary>
/// Infrastructure implementation of ISubscriptionService.
/// Reads subscription + plan data directly from the database.
/// NOT cached — plan limits must always reflect the current plan.
/// </summary>
public sealed class SubscriptionService : ISubscriptionService
{
    private readonly IApplicationDbContext _context;

    public SubscriptionService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CurrentPlanInfo?> GetCurrentPlanAsync(
        Guid retailerId,
        CancellationToken ct = default)
    {
        var result = await _context.Subscriptions
            .AsNoTracking()
            .Where(s => s.RetailerId == retailerId
                     && (s.Status == SubscriptionStatus.Active
                      || s.Status == SubscriptionStatus.Trial))
            .Join(
                _context.SubscriptionPlans.AsNoTracking(),
                s => s.PlanId,
                sp => sp.Id,
                (s, sp) => new CurrentPlanInfo(
                    sp.Id,
                    sp.Name,
                    sp.Tier,
                    sp.MaxActiveProducts,
                    sp.MaxMonthlyTryOns))
            .FirstOrDefaultAsync(ct);

        return result;
    }
}