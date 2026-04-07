using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;


namespace Application.Features.Dashboard.Queries.GetReturnReasons;

public sealed class GetReturnReasonsQueryHandler
    : IRequestHandler<GetReturnReasonsQuery, List<ReturnReasonDto>>
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetReturnReasonsQueryHandler(
        IDashboardRepository dashboardRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _dashboardRepository = dashboardRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<List<ReturnReasonDto>> Handle(
        GetReturnReasonsQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"dashboard:{retailerId}:returnreasons:{query.From:yyyy-MM-dd}:{query.To:yyyy-MM-dd}";

        List<ReturnReasonDto>? cached =
            await _cacheService.GetAsync<List<ReturnReasonDto>>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        List<ReturnReasonDto> result = await _dashboardRepository.GetReturnReasonsAsync(
            retailerId, query.From, query.To, cancellationToken);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        return result;
    }
}