using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using System.Collections.Concurrent;

namespace Application.Features.Dashboard.Queries.GetKpis;

/// <summary>
/// Fetches dashboard KPIs via IDashboardRepository.
/// Cache-stampede prevention: per-key SemaphoreSlim stored in a static ConcurrentDictionary.
/// Cache TTL: 5 minutes.
/// </summary>

public sealed class GetKpisQueryHandler : IRequestHandler<GetKpisQuery, KpiDto>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks =
        new(StringComparer.Ordinal);

    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetKpisQueryHandler(
        IDashboardRepository dashboardRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _dashboardRepository = dashboardRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<KpiDto> Handle(
        GetKpisQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"dashboard:{retailerId}:kpis:{query.From:yyyy-MM-dd}:{query.To:yyyy-MM-dd}";

        KpiDto? cached = await _cacheService.GetAsync<KpiDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        SemaphoreSlim gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            cached = await _cacheService.GetAsync<KpiDto>(cacheKey, cancellationToken);
            if (cached is not null)
                return cached;

            KpiDto result = await _dashboardRepository.GetKpisAsync(
                retailerId, query.From, query.To, cancellationToken);

            await _cacheService.SetAsync(
                cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);

            return result;
        }
        finally
        {
            gate.Release();
        }
    }
}