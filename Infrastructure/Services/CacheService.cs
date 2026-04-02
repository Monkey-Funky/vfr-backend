using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Infrastructure.Services;

public sealed class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;

    public CacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var cached = await _cache.GetStringAsync(key, cancellationToken);

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
        var options = new DistributedCacheEntryOptions
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

    public Task RemoveByPrefixAsync(
        string prefix,
        CancellationToken cancellationToken = default)
    {
        // Note: IDistributedCache has no built-in prefix removal.
        // If Redis is configured, this can be implemented with IConnectionMultiplexer.
        // For now this is a no-op stub; P-049 (Polly / resilience) documents the full impl.
        return Task.CompletedTask;
    }
}