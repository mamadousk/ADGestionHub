// ==================== FILE: Services/StockService.cs ====================
using AdGestionHub.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AdGestionHub.Services
{
    public class StockService : IStockService
    {
        private readonly ApplicationDbContext _context;

        public StockService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> CheckStockAvailabilityAsync(int productId, int quantityRequested)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
                return false;

            return product.StockQuantity >= quantityRequested;
        }

        public async Task<bool> DeductStockAsync(int productId, int quantity)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null || product.StockQuantity < quantity)
                return false;

            product.StockQuantity -= quantity;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RestoreStockAsync(int productId, int quantity)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
                return false;

            product.StockQuantity += quantity;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetLowStockCountAsync(int boutiqueId)
        {
            return await _context.Products
                .AsNoTracking()
                .CountAsync(p => p.BoutiqueId == boutiqueId && p.StockQuantity <= p.LowStockThreshold);
        }
    }
}