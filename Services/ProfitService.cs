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
            // UnitPrice est toujours enregistré hors taxe (Net HT), et les ventes annulées sont exclues.
            var itemsQuery =
                from si in _context.SaleItems.AsNoTracking()
                join s in _context.Sales.AsNoTracking() on si.SaleId equals s.Id
                join p in _context.Products.AsNoTracking().Where(pr => pr.BoutiqueId == boutiqueId)
                    on si.ProductId equals (int?)p.Id into prodGroup
                from p in prodGroup.DefaultIfEmpty()
                where s.BoutiqueId == boutiqueId
                      && !s.IsCancelled
                      && (!startDate.HasValue || s.SaleDate >= startDate.Value)
                      && (!endDate.HasValue || s.SaleDate <= endDate.Value)
                select new
                {
                    si.Quantity,
                    si.UnitPrice,
                    EffectiveCost = si.UnitCost > 0m ? si.UnitCost : (p != null ? p.PurchasePrice : 0m)
                };

            return await itemsQuery.SumAsync(x => (x.UnitPrice - x.EffectiveCost) * x.Quantity);
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
