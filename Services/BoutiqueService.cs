using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AdGestionHub.Services
{
    public class BoutiqueService : IBoutiqueService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;

        public BoutiqueService(UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor, ApplicationDbContext context)
        {
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        public async Task<Boutique> GetCurrentBoutiqueAsync()
        {
            var userId = _userManager.GetUserId(_httpContextAccessor.HttpContext.User);
            if (string.IsNullOrEmpty(userId))
                return null;

            var user = await _userManager.Users
                .Include(u => u.Boutique)
                .FirstOrDefaultAsync(u => u.Id == userId);

            return user?.Boutique;
        }

        public async Task<int?> GetCurrentBoutiqueIdAsync()
        {
            var boutique = await GetCurrentBoutiqueAsync();
            return boutique?.Id;
        }
    }
}