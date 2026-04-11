using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Dashboard.Queries.GetRealTimeActivity;

/// <summary>
/// CRITICAL: ICacheService is intentionally NOT injected here.
/// Real-time activity must always read from the database — never cached.
/// </summary>
public sealed class GetRealTimeActivityQueryHandler
    : IRequestHandler<GetRealTimeActivityQuery, List<ActivityEventDto>>
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICurrentUserService _currentUserService;

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

        // Direct DB query — NO cache. Uses (retailer_id, created_at DESC) index.
        return await _dashboardRepository.GetRealTimeActivityAsync(
            retailerId, cancellationToken);
    }
}