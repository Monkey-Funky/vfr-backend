using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;


namespace Application.Features.Dashboard.Queries.GetSessionsChart;

public sealed class GetSessionsChartQueryHandler
    : IRequestHandler<GetSessionsChartQuery, List<ChartDataPoint>>
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetSessionsChartQueryHandler(
        IDashboardRepository dashboardRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _dashboardRepository = dashboardRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<List<ChartDataPoint>> Handle(
        GetSessionsChartQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"dashboard:{retailerId}:sessions:{query.From:yyyy-MM-dd}:{query.To:yyyy-MM-dd}:{query.GroupBy}";

        List<ChartDataPoint>? cached =
            await _cacheService.GetAsync<List<ChartDataPoint>>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        List<ChartDataPoint> result = await _dashboardRepository.GetSessionsChartAsync(
            retailerId, query.From, query.To, query.GroupBy, cancellationToken);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30), cancellationToken);

        return result;
    }
}