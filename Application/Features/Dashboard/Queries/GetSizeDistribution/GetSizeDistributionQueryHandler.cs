using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Dashboard.Queries.GetSizeDistribution;

public sealed class GetSizeDistributionQueryHandler
    : IRequestHandler<GetSizeDistributionQuery, List<SizeDistributionDto>>
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetSizeDistributionQueryHandler(
        IDashboardRepository dashboardRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _dashboardRepository = dashboardRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<List<SizeDistributionDto>> Handle(
        GetSizeDistributionQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"dashboard:{retailerId}:sizedist:{query.From:yyyy-MM-dd}:{query.To:yyyy-MM-dd}";

        List<SizeDistributionDto>? cached =
            await _cacheService.GetAsync<List<SizeDistributionDto>>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        List<SizeDistributionDto> result = await _dashboardRepository.GetSizeDistributionAsync(
            retailerId, query.From, query.To, cancellationToken);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        return result;
    }
}