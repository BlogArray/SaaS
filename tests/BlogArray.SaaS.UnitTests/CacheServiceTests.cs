using BlogArray.SaaS.Domain.DTOs;
using BlogArray.SaaS.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class CacheServiceTests
{
    private static CacheService CreateService()
    {
        return new CacheService(new FakeDistributedCache(), Options.Create(new CacheConfiguration
        {
            AbsoluteExpirationInHours = 1,
            SlidingExpirationInMinutes = 30
        }));
    }

    [Fact]
    public void Set_ThenTryGet_RoundTripsValue()
    {
        CacheService cache = CreateService();

        cache.Set("tenant:1", "acme");

        Assert.True(cache.TryGet("tenant:1", out string value));
        Assert.Equal("acme", value);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        CacheService cache = CreateService();

        Assert.False(cache.TryGet("missing", out string _));
    }

    [Fact]
    public async Task GetOrCreateAsync_CachesFactoryResult()
    {
        CacheService cache = CreateService();
        int factoryCalls = 0;

        string first = await cache.GetOrCreateAsync("key", () =>
        {
            factoryCalls++;
            return Task.FromResult("value");
        });

        string second = await cache.GetOrCreateAsync("key", () =>
        {
            factoryCalls++;
            return Task.FromResult("value");
        });

        Assert.Equal("value", first);
        Assert.Equal("value", second);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task RemoveAsync_DeletesEntry()
    {
        CacheService cache = CreateService();

        await cache.SetAsync("key", "value");
        await cache.RemoveAsync("key");

        Assert.False(cache.TryGet("key", out string _));
    }

    private sealed class FakeDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = [];

        public byte[]? Get(string key)
        {
            return _store.TryGetValue(key, out byte[]? value) ? value : null;
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Get(key));
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            _store.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            _store[key] = value;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
    }
}
