// ==================== FILE: Services/ProfitService.cs ====================
using AdGestionHub.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AdGestionHub.Services
{
    public class ProfitService : IProfitService
    {
        private readonly ApplicationDbContext _context;

        public ProfitService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> CalculateProfitAsync(int boutiqueId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var grossProfit = await CalculateGrossProfitAsync(boutiqueId, startDate, endDate);
            var expenses = await GetTotalExpensesAsync(boutiqueId, startDate, endDate);
            return grossProfit - expenses;
        }

        public async Task<decimal> CalculateGrossProfitAsync(int boutiqueId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Sales
                .AsNoTracking()
                .Include(s => s.Items)
                .Where(s => s.BoutiqueId == boutiqueId);

            if (startDate.HasValue)
                query = query.Where(s => s.SaleDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(s => s.SaleDate <= endDate.Value);

            var sales = await query.ToListAsync();

            decimal grossProfit = 0;
            foreach (var sale in sales)
            {
                foreach (var item in sale.Items)
                {
                    // Récupérer le prix d'achat du produit (si disponible)
                    var product = await _context.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId && p.BoutiqueId == boutiqueId);

                    decimal purchaseCost = (product?.PurchasePrice ?? 0) * item.Quantity;
                    grossProfit += (item.UnitPrice * item.Quantity) - purchaseCost;
                }
            }

            return grossProfit;
        }

        private async Task<decimal> GetTotalExpensesAsync(int boutiqueId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Expenses
                .AsNoTracking()
                .Where(e => e.BoutiqueId == boutiqueId);

            if (startDate.HasValue)
                query = query.Where(e => e.Date >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(e => e.Date <= endDate.Value);

            return await query.SumAsync(e => e.Amount);
        }
    }
}