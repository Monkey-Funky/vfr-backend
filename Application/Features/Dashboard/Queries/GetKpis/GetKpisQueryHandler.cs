

using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using System.Collections.Concurrent;

namespace Application.Features.Dashboard.Queries.GetKpis;


/// <summary>
/// Handles GetKpisQuery with cache-stampede prevention.
///
/// Cache key  : "dashboard:{retailerId}:kpis:{from}:{to}"
/// Cache TTL  : 5 minutes (KPIs are the most read, shortest-lived dashboard data)
/// Locking    : Per-key SemaphoreSlim stored in a static ConcurrentDictionary.
///              On a cache miss, only one concurrent caller queries the DB;
///              all others wait on the semaphore and re-read from cache on entry.
///
/// Why static locks: The handler is registered as transient (per-request) by MediatR.
///   Instance-level semaphores would not protect across concurrent HTTP requests.
///   A static ConcurrentDictionary provides process-wide, per-key mutual exclusion.
///
/// Memory management: Semaphores are never removed from the dictionary.
///   In practice the number of distinct {from}:{to} keys is bounded by the date
///   range allowed (max 365 days, max 2 years lookback), and entries are tiny
///   (SemaphoreSlim is ~40 bytes). This is acceptable for an API process lifetime.
/// </summary>
public sealed class GetKpisQueryHandler : IRequestHandler<GetKpisQuery, KpiDto>
{
    // ── Per-key lock registry ─────────────────────────────────────────────────
    // Static so that all handler instances (one per request) share the same locks.
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

        // ── Fast path: cache hit (no lock needed) ────────────────────────────
        KpiDto? cached = await _cacheService.GetAsync<KpiDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        // ── Slow path: cache miss — acquire per-key lock ──────────────────────
        // GetOrAdd is atomic: the semaphore for this key is created exactly once.
        SemaphoreSlim gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);
        try
        {
            // Double-checked locking: another waiter may have populated the cache
            // while this request was waiting on the semaphore.
            cached = await _cacheService.GetAsync<KpiDto>(cacheKey, cancellationToken);
            if (cached is not null)
                return cached;

            // Only one request reaches here per cache key — safe to query DB.
            KpiDto result = await _dashboardRepository.GetKpisAsync(
                retailerId, query.From, query.To, cancellationToken);

            await _cacheService.SetAsync(
                cacheKey,
                result,
                TimeSpan.FromMinutes(5),
                cancellationToken);

            return result;
        }
        finally
        {
            // Always release — even if DB query throws.
            gate.Release();
        }
    }
}