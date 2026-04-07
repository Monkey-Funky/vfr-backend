using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Dashboard.Queries.GetRealTimeActivity;

/// <summary>
/// Returns the 20 most recent activity events for the authenticated retailer.
///
/// CACHE RULE: This handler must NEVER call ICacheService.
/// Real-time activity data is stale the moment it is cached.
/// The query uses the (retailer_id, created_at DESC) index for performance.
/// </summary>
public sealed class GetRealTimeActivityQueryHandler
    : IRequestHandler<GetRealTimeActivityQuery, List<ActivityEventDto>>
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICurrentUserService _currentUserService;

    // Note: ICacheService is intentionally NOT injected in this handler.
    public GetRealTimeActivityQueryHandler(
        IDashboardRepository dashboardRepository,
        ICurrentUserService currentUserService)
    {
        _dashboardRepository = dashboardRepository;
        _currentUserService = currentUserService;
    }

    public async Task<List<ActivityEventDto>> Handle(
        GetRealTimeActivityQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // Direct DB query — no cache. Uses (retailer_id, created_at DESC) index.
        return await _dashboardRepository.GetRealTimeActivityAsync(
            retailerId, cancellationToken);
    }
}