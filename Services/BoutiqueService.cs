using AdGestionHub.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AdGestionHub.Services
{
    public class BoutiqueService : IBoutiqueService
    {
        private const string CacheItemKey = "__CurrentBoutique";
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BoutiqueService(UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<Boutique?> GetCurrentBoutiqueAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
                return null;

            if (httpContext.Items.TryGetValue(CacheItemKey, out var cached) && cached is Boutique cachedBoutique)
                return cachedBoutique;

            var userId = _userManager.GetUserId(httpContext.User);
            if (string.IsNullOrEmpty(userId))
                return null;

            var user = await _userManager.Users
                .AsNoTracking()
                .Include(u => u.Boutique)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.Boutique != null)
            {
                httpContext.Items[CacheItemKey] = user.Boutique;
            }

            return user?.Boutique;
        }

        public async Task<int?> GetCurrentBoutiqueIdAsync()
        {
            var boutique = await GetCurrentBoutiqueAsync();
            return boutique?.Id;
        }
    }
}
