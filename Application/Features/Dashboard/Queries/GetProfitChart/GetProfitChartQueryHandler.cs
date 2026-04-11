using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Dashboard.Queries.GetProfitChart;

public sealed class GetProfitChartQueryHandler
    : IRequestHandler<GetProfitChartQuery, List<ChartDataPoint>>
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetProfitChartQueryHandler(
        IDashboardRepository dashboardRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _dashboardRepository = dashboardRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<List<ChartDataPoint>> Handle(
        GetProfitChartQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"dashboard:{retailerId}:profit:{query.From:yyyy-MM-dd}:{query.To:yyyy-MM-dd}:{query.GroupBy}";

        List<ChartDataPoint>? cached =
            await _cacheService.GetAsync<List<ChartDataPoint>>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        List<ChartDataPoint> result = await _dashboardRepository.GetProfitChartAsync(
            retailerId, query.From, query.To, query.GroupBy, cancellationToken);

        await _cacheService.SetAsync(
            cacheKey, result, TimeSpan.FromMinutes(30), cancellationToken);

        return result;
    }
}