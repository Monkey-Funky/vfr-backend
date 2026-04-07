using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Dashboard.Queries.GetTryOnEngagement;

public sealed class GetTryOnEngagementQueryHandler
    : IRequestHandler<GetTryOnEngagementQuery, List<TryOnEngagementDto>>
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetTryOnEngagementQueryHandler(
        IDashboardRepository dashboardRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _dashboardRepository = dashboardRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<List<TryOnEngagementDto>> Handle(
        GetTryOnEngagementQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"dashboard:{retailerId}:tryonengagement:{query.From:yyyy-MM-dd}:{query.To:yyyy-MM-dd}:{query.GroupBy}";

        List<TryOnEngagementDto>? cached =
            await _cacheService.GetAsync<List<TryOnEngagementDto>>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        List<TryOnEngagementDto> result = await _dashboardRepository.GetTryOnEngagementAsync(
            retailerId, query.From, query.To, query.GroupBy, cancellationToken);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30), cancellationToken);

        return result;
    }
}