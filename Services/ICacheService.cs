// ==================== FILE: Services/ICacheService.cs ====================
using System;
using System.Threading.Tasks;

namespace AdGestionHub.Services
{
    public interface ICacheService
    {
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);
        Task RemoveAsync(string key);
        bool TryGet<T>(string key, out T value);
        void Set<T>(string key, T value, TimeSpan? expiry = null);
    }
}