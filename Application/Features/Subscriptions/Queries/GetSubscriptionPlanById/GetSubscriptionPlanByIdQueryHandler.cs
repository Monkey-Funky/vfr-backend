using Application.Features.Subscriptions.DTOs;
using Application.Interfaces.Persistence;
using Domain.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Subscriptions.Queries.GetSubscriptionPlanById;

public sealed class GetSubscriptionPlanByIdQueryHandler
    : IRequestHandler<GetSubscriptionPlanByIdQuery, SubscriptionPlanDto>
{
    private readonly IApplicationDbContext _context;

    public GetSubscriptionPlanByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SubscriptionPlanDto> Handle(
        GetSubscriptionPlanByIdQuery query,
        CancellationToken cancellationToken)
    {
        SubscriptionPlan plan = await _context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.PlanId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), query.PlanId);

        return plan.ToDto();
    }
}