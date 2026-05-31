using Application.Features.Subscriptions.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Subscriptions.Queries.GetSubscriptionPlanById;

/// <summary>
/// Returns a subscription plan by ID.
/// Cache-aside: TTL 60 minutes. Plans rarely change.
/// </summary>
public sealed class GetSubscriptionPlanByIdQueryHandler
    : IRequestHandler<GetSubscriptionPlanByIdQuery, SubscriptionPlanDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public GetSubscriptionPlanByIdQueryHandler(
        IApplicationDbContext context,
        ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<SubscriptionPlanDto> Handle(
        GetSubscriptionPlanByIdQuery query,
        CancellationToken cancellationToken)
    {
        string cacheKey = $"sub_plan:{query.PlanId:N}";

        var cached = await _cacheService.GetAsync<SubscriptionPlanDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        SubscriptionPlan plan = await _context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.PlanId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), query.PlanId);

        var dto = plan.ToDto();

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(60), cancellationToken);

        return dto;
    }
}
