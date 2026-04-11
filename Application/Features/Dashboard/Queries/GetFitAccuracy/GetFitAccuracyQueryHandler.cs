using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Dashboard.Queries.GetFitAccuracy;

public sealed class GetFitAccuracyQueryHandler
    : IRequestHandler<GetFitAccuracyQuery, FitAccuracyDto>
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetFitAccuracyQueryHandler(
        IDashboardRepository dashboardRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _dashboardRepository = dashboardRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<FitAccuracyDto> Handle(
        GetFitAccuracyQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"dashboard:{retailerId}:fitaccuracy:{query.From:yyyy-MM-dd}:{query.To:yyyy-MM-dd}";

        FitAccuracyDto? cached =
            await _cacheService.GetAsync<FitAccuracyDto>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        FitAccuracyDto result = await _dashboardRepository.GetFitAccuracyAsync(
            retailerId, query.From, query.To, cancellationToken);

        await _cacheService.SetAsync(
            cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        return result;
    }
}