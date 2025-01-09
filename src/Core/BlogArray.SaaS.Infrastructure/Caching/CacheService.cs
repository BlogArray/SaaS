using BlogArray.SaaS.Domain.DTOs;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BlogArray.SaaS.Infrastructure.Caching;

public interface ICacheService
{
    /// <summary>
    /// Attempts to retrieve a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="cacheKey">The cache key used to retrieve the value.</param>
    /// <param name="value">The value retrieved from the cache, if found.</param>
    /// <returns>True if the value was found in the cache, otherwise false.</returns>
    bool TryGet<T>(string cacheKey, out T value);

    /// <summary>
    /// Attempts to retrieve a value from the cache asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="cacheKey">The cache key used to retrieve the value.</param>
    /// <returns>A tuple containing a boolean indicating success and the value retrieved from the cache, if found.</returns>
    Task<(bool, T)> TryGetAsync<T>(string cacheKey);

    /// <summary>
    /// Sets a value in the cache with the specified key and expiration settings.
    /// </summary>
    /// <typeparam name="T">The type of the value to be cached.</typeparam>
    /// <param name="cacheKey">The cache key used to store the value.</param>
    /// <param name="value">The value to be cached.</param>
    /// <returns>The value that was cached.</returns>
    T Set<T>(string cacheKey, T value);

    /// <summary>
    /// Sets a value in the cache with the specified key and expiration settings.
    /// </summary>
    /// <typeparam name="T">The type of the value to be cached.</typeparam>
    /// <summary>
    /// Sets a value in the cache with the specified key and expiration settings asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the value to be cached.</typeparam>
    /// <param name="cacheKey">The cache key used to store the value.</param>
    /// <param name="value">The value to be cached.</param>
    /// <returns>The value that was cached.</returns>
    Task<T> SetAsync<T>(string cacheKey, T value);

    /// <summary>
    /// Tries to get the value from the cache. If it doesn't exist, it will create, cache, and return the value asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the object to cache.</typeparam>
    /// <param name="cacheKey">The cache key to search for.</param>
    /// <param name="factory">The factory function to generate the value if it doesn't exist.</param>
    /// <returns>The cached value or the value created by the factory.</returns>
    Task<T> GetOrCreateAsync<T>(string cacheKey, Func<Task<T>> factory);

    /// <summary>
    /// Removes the specified cache entry from the cache.
    /// </summary>
    /// <param name="cacheKey">The cache key to remove.</param>
    void Remove(string cacheKey);

    /// <summary>
    /// Removes the specified cache entry from the cache.
    /// </summary>
    /// <param name="cacheKey">The cache key to remove.</param>
    Task RemoveAsync(string cacheKey);
}

/// <summary>
/// Initializes a new instance of the <see cref="RedisCacheService"/> class.
/// Configures the cache expiration based on the provided configuration.
/// </summary>
/// <param name="distributedCache">The Redis cache instance.</param>
/// <param name="cacheConfig">The cache configuration options.</param>
public class CacheService(IDistributedCache distributedCache, IOptions<CacheConfiguration> cacheConfig) : ICacheService
{

    /// <summary>
    /// Sets a value in the cache with the specified key and expiration settings.
    /// </summary>
    /// <typeparam name="T">The type of the value to be cached.</typeparam>
    private DistributedCacheEntryOptions GetCacheEntryOptions()
    {
        return new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(cacheConfig.Value.AbsoluteExpirationInHours),
            SlidingExpiration = TimeSpan.FromMinutes(cacheConfig.Value.SlidingExpirationInMinutes)
        };
    }

    /// <summary>
    /// Attempts to retrieve a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="cacheKey">The cache key used to retrieve the value.</param>
    /// <param name="value">The value retrieved from the cache, if found.</param>
    /// <returns>True if the value was found in the cache, otherwise false.</returns>
    public bool TryGet<T>(string cacheKey, out T value)
    {
        string? cachedValue = distributedCache.GetString(cacheKey);
        if (!string.IsNullOrEmpty(cachedValue))
        {
            value = JsonSerializer.Deserialize<T>(cachedValue)!;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Attempts to retrieve a value from the cache asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="cacheKey">The cache key used to retrieve the value.</param>
    /// <returns>A tuple containing a boolean indicating success and the value retrieved from the cache, if found.</returns>
    public async Task<(bool, T)> TryGetAsync<T>(string cacheKey)
    {
        string? cachedValue = await distributedCache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedValue))
        {
            T value = JsonSerializer.Deserialize<T>(cachedValue)!;
            return (true, value);
        }

        return (false, default!);
    }

    /// <summary>
    /// Sets a value in the cache with the specified key and expiration settings.
    /// </summary>
    /// <typeparam name="T">The type of the value to be cached.</typeparam>
    /// <param name="cacheKey">The cache key used to store the value.</param>
    /// <param name="value">The value to be cached.</param>
    /// <returns>The value that was cached.</returns>
    public T Set<T>(string cacheKey, T value)
    {
        string serializedValue = JsonSerializer.Serialize(value);

        distributedCache.SetString(cacheKey, serializedValue, GetCacheEntryOptions());
        return value;
    }

    /// <summary>
    /// Sets a value in the cache with the specified key and expiration settings.
    /// </summary>
    /// <typeparam name="T">The type of the value to be cached.</typeparam>
    /// <summary>
    /// Sets a value in the cache with the specified key and expiration settings asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the value to be cached.</typeparam>
    /// <param name="cacheKey">The cache key used to store the value.</param>
    /// <param name="value">The value to be cached.</param>
    /// <returns>The value that was cached.</returns>
    public async Task<T> SetAsync<T>(string cacheKey, T value)
    {
        string serializedValue = JsonSerializer.Serialize(value);
        await distributedCache.SetStringAsync(cacheKey, serializedValue, GetCacheEntryOptions());
        return value;
    }

    /// <summary>
    /// Tries to get the value from the cache. If it doesn't exist, it will create, cache, and return the value asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the object to cache.</typeparam>
    /// <param name="cacheKey">The cache key to search for.</param>
    /// <param name="factory">The factory function to generate the value if it doesn't exist.</param>
    /// <returns>The cached value or the value created by the factory.</returns>
    public async Task<T> GetOrCreateAsync<T>(string cacheKey, Func<Task<T>> factory)
    {
        string? cachedValue = await distributedCache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedValue))
        {
            return JsonSerializer.Deserialize<T>(cachedValue)!;
        }

        // Value not found in cache, create it using the factory
        T value = await factory();

        // Store it in cache
        await SetAsync(cacheKey, value);

        return value;
    }

    /// <summary>
    /// Removes the specified cache entry from the cache.
    /// </summary>
    /// <param name="cacheKey">The cache key to remove.</param>
    public void Remove(string cacheKey)
    {
        distributedCache.Remove(cacheKey);
    }

    /// <summary>
    /// Removes the specified cache entry from the cache.
    /// </summary>
    /// <param name="cacheKey">The cache key to remove.</param>
    public async Task RemoveAsync(string cacheKey)
    {
        await distributedCache.RemoveAsync(cacheKey);
    }
}
