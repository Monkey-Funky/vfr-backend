using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using System.Collections.Concurrent;

namespace Application.Features.Dashboard.Queries.GetKpis;

/// <summary>
/// Fetches dashboard KPIs via IDashboardRepository.
/// Cache-stampede prevention: per-key SemaphoreSlim stored in a static ConcurrentDictionary.
/// Cache TTL: 5 minutes.
///
/// BUGFIX (unbounded memory growth): the cache key includes the exact From/To dates,
/// so every distinct date range any retailer has ever requested used to leave a
/// permanent SemaphoreSlim entry in the static dictionary — it was added via
/// GetOrAdd but never removed, so the dictionary grew for the lifetime of the
/// process. The finally block now does a best-effort removal of the semaphore once
/// nobody else is waiting on it, so keys for one-off/rarely-reused date ranges don't
/// accumulate forever. This is intentionally best-effort (not perfectly race-free):
/// in the rare case a new waiter arrives in the tiny window between the count check
/// and the removal, it simply creates a fresh semaphore for that key, which only
/// costs an extra cache-stampede check — it never causes incorrect KPI data.
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

            // Best-effort cleanup to stop the dictionary growing forever: only
            // remove the entry if nobody else is currently queued behind it.
            if (gate.CurrentCount == 1)
            {
                _locks.TryRemove(cacheKey, out _);
            }
        }
    }
}