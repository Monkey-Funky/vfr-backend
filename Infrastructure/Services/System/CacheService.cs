using Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;

namespace Infrastructure.Services.System;

/// <summary>
/// Redis-backed implementation of ICacheService.
///
/// P-041 CHANGE: Added RemoveByPatternAsync using IConnectionMultiplexer.
///   IDistributedCache cannot do pattern-based key removal.
///   IConnectionMultiplexer.GetDatabase() gives direct Redis access for SCAN + DEL.
///
/// BUG FIX: IDistributedCache prepends InstanceName ("vfr:") to every key it stores,
///   but IConnectionMultiplexer operates on raw Redis keys without any prefix.
///   RemoveByPatternAsync must therefore prepend _instanceName to the pattern so that
///   the SCAN command matches the actual keys in Redis.
/// </summary>
public sealed class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly string _instanceName;

    public CacheService(
        IDistributedCache cache,
        IConnectionMultiplexer multiplexer,
        IOptions<RedisCacheOptions> redisOptions)
    {
        _cache = cache;
        _multiplexer = multiplexer;
        _instanceName = redisOptions.Value.InstanceName ?? string.Empty;
    }

    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
        where T : class
    {
        string? cached = await _cache.GetStringAsync(key, cancellationToken);

        return cached is null
            ? null
            : JsonSerializer.Deserialize<T>(cached);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        DistributedCacheEntryOptions options = new()
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromHours(1)
        };

        await _cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(value),
            options,
            cancellationToken);
    }

    public async Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
        => await _cache.RemoveAsync(key, cancellationToken);

    public async Task RemoveByPrefixAsync(
        string prefix,
        CancellationToken cancellationToken = default)
        => await RemoveByPatternAsync($"{_instanceName}{prefix}*", cancellationToken);

    /// <summary>
    /// Removes all keys matching the given glob pattern using Redis SCAN + DEL.
    ///
    /// Implementation:
    ///   1. SCAN iterates all matching keys in the Redis keyspace in batches of 250.
    ///      SCAN is non-blocking and O(1) per call — preferable to KEYS which is O(N)
    ///      and blocks the Redis server for the full scan.
    ///   2. For each batch of matching keys, execute a pipelined DEL command.
    ///   3. Continue until SCAN returns cursor = 0 (full iteration complete).
    ///
    /// CAUTION: SCAN still iterates the entire keyspace for pattern matching.
    ///   Call this only in response to meaningful business events, not on every request.
    /// </summary>
    public async Task RemoveByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default)
    {
        IDatabase db = _multiplexer.GetDatabase();
        IServer server = _multiplexer.GetServer(
            _multiplexer.GetEndPoints().First());

        // Batch keys for pipelined DEL (avoids N round-trips).
        List<RedisKey> batch = new(capacity: 250);

        await foreach (RedisKey key in server.KeysAsync(pattern: pattern))
        {
            cancellationToken.ThrowIfCancellationRequested();

            batch.Add(key);

            if (batch.Count >= 250)
            {
                await db.KeyDeleteAsync(batch.ToArray());
                batch.Clear();
            }
        }

        // Delete any remainder.
        if (batch.Count > 0)
            await db.KeyDeleteAsync(batch.ToArray());
    }
}