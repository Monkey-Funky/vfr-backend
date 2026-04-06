using Application.Features.Subscriptions.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Subscriptions.Queries.GetAllSubscriptionPlans;

public sealed class GetAllSubscriptionPlansQueryHandler
    : IRequestHandler<GetAllSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanGroupDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public GetAllSubscriptionPlansQueryHandler(
        IApplicationDbContext context,
        ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyList<SubscriptionPlanGroupDto>> Handle(
        GetAllSubscriptionPlansQuery query,
        CancellationToken cancellationToken)
    {
        // Cache key per 04-CQRSConventions §9.3 and CacheKeys helper.
        // Key pattern: "subscription_plans:{billingCycle}"
        string cacheKey = CacheKeys.SubscriptionPlans(query.BillingCycle ?? "all");

        IReadOnlyList<SubscriptionPlanGroupDto>? cached =
            await _cacheService.GetAsync<IReadOnlyList<SubscriptionPlanGroupDto>>(
                cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        // Cache miss — query the database.
        IQueryable<SubscriptionPlan> queryable = _context.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive);

        // Apply optional billing cycle filter.
        if (!string.IsNullOrWhiteSpace(query.BillingCycle))
            queryable = queryable.Where(p => p.BillingCycle == query.BillingCycle);

        List<SubscriptionPlan> plans = await queryable
            .OrderBy(p => p.Tier)
            .ThenBy(p => p.PriceAmount)
            .ToListAsync(cancellationToken);

        // Group by Tier for the UI pricing page layout.
        IReadOnlyList<SubscriptionPlanGroupDto> result = plans
            .GroupBy(p => p.Tier)
            .Select(g => new SubscriptionPlanGroupDto(
                Tier: g.Key,
                Plans: g.Select(p => p.ToDto()).ToList().AsReadOnly()))
            .ToList()
            .AsReadOnly();

        // Store in Redis — TTL 1 hour (plans change infrequently).
        await _cacheService.SetAsync(
            cacheKey,
            result,
            TimeSpan.FromHours(1),
            cancellationToken);

        return result;
    }
}