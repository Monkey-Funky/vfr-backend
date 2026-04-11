using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;


namespace Application.Features.Dashboard.Queries.GetConversionRate;

public sealed class GetConversionRateQueryHandler
    : IRequestHandler<GetConversionRateQuery, ConversionRateDto>
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetConversionRateQueryHandler(
        IDashboardRepository dashboardRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _dashboardRepository = dashboardRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<ConversionRateDto> Handle(
        GetConversionRateQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"dashboard:{retailerId}:conversion:{query.From:yyyy-MM-dd}:{query.To:yyyy-MM-dd}";

        ConversionRateDto? cached =
            await _cacheService.GetAsync<ConversionRateDto>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        ConversionRateDto result = await _dashboardRepository.GetConversionRateAsync(
            retailerId, query.From, query.To, cancellationToken);

        await _cacheService.SetAsync(
            cacheKey, result, TimeSpan.FromMinutes(30), cancellationToken);

        return result;
    }
}