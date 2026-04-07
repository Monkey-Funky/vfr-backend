namespace Application.Interfaces.Services;

/// <summary>
/// Distributed cache abstraction (Redis-backed in production).
/// Query handlers use the Cache-Aside pattern: check → miss → query → store.
///
/// CHANGES IN P-041:
///   • Added RemoveByPatternAsync — uses Redis SCAN + DEL to invalidate all keys
///     matching a glob pattern (e.g. "dashboard:{retailerId}:*").
///     Required for dashboard cache invalidation on OrderDelivered events.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class;

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
        where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all Redis keys whose names match the given glob pattern.
    /// Implemented via SCAN + DEL in CacheService.
    /// Example pattern: "dashboard:{retailerId}:*"
    ///
    /// WARNING: SCAN iterates the full Redis keyspace. Use sparingly and only
    /// in response to meaningful business events, not on every request.
    /// </summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
}