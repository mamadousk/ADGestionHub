// ==================== FILE: Services/CacheService.cs ====================
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading.Tasks;

namespace AdGestionHub.Services
{
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;

        public CacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
        {
            if (_cache.TryGetValue(key, out T cachedValue))
                return cachedValue;

            var result = await factory();
            var cacheOptions = new MemoryCacheEntryOptions();
            if (expiry.HasValue)
                cacheOptions.SetAbsoluteExpiration(expiry.Value);
            else
                cacheOptions.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(key, result, cacheOptions);
            return result;
        }

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }

        public bool TryGet<T>(string key, out T value)
        {
            return _cache.TryGetValue(key, out value);
        }

        public void Set<T>(string key, T value, TimeSpan? expiry = null)
        {
            var cacheOptions = new MemoryCacheEntryOptions();
            if (expiry.HasValue)
                cacheOptions.SetAbsoluteExpiration(expiry.Value);
            else
                cacheOptions.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(key, value, cacheOptions);
        }
    }
}