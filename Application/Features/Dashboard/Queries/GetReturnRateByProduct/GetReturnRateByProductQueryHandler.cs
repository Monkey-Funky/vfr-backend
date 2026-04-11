using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Dashboard.Queries.GetReturnRateByProduct;

public sealed class GetReturnRateByProductQueryHandler
    : IRequestHandler<GetReturnRateByProductQuery, List<ReturnRateByProductDto>>
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetReturnRateByProductQueryHandler(
        IDashboardRepository dashboardRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _dashboardRepository = dashboardRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<List<ReturnRateByProductDto>> Handle(
        GetReturnRateByProductQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"dashboard:{retailerId}:returnratebyproduct:{query.From:yyyy-MM-dd}:{query.To:yyyy-MM-dd}";

        List<ReturnRateByProductDto>? cached =
            await _cacheService.GetAsync<List<ReturnRateByProductDto>>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        List<ReturnRateByProductDto> result = await _dashboardRepository.GetReturnRateByProductAsync(
            retailerId, query.From, query.To, cancellationToken);

        await _cacheService.SetAsync(
            cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        return result;
    }
}