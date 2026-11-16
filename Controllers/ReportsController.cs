using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProfitService _profitService;
        private readonly IPdfReportService _pdfReportService;

        public ReportsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IProfitService profitService,
            IPdfReportService pdfReportService)
        {
            _context = context;
            _userManager = userManager;
            _profitService = profitService;
            _pdfReportService = pdfReportService;
        }

        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
            {
                if (User.IsInRole("SuperAdmin"))
                    return RedirectToAction("Index", "SuperAdmin");
                return Challenge();
            }

            var model = await BuildReportAsync(user.BoutiqueId.Value, startDate, endDate);
            return View(model);
        }

        public async Task<IActionResult> GenerateDailyReport(DateTime? date, DateTime? startDate, DateTime? endDate)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var effectiveStart = startDate ?? date ?? DateTime.Today;
            var effectiveEnd = endDate ?? date ?? effectiveStart;

            var model = await BuildReportAsync(user.BoutiqueId.Value, effectiveStart, effectiveEnd);
            var pdfBytes = _pdfReportService.GeneratePeriodReportPdf(model);

            var fileName = model.StartDate.Date == model.EndDate.Date
                ? $"Rapport_{model.StartDate:yyyyMMdd}.pdf"
                : $"Rapport_{model.StartDate:yyyyMMdd}_{model.EndDate:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        private async Task<ReportViewModel> BuildReportAsync(int boutiqueId, DateTime? startDate, DateTime? endDate)
        {
            var start = (startDate ?? DateTime.Today).Date;
            var end = (endDate ?? start).Date;
            if (end < start)
                (start, end) = (end, start);

            var endExclusive = end.AddDays(1);

            var sales = await _context.Sales
                .AsNoTracking()
                .Include(s => s.Items)
                .Where(s => s.BoutiqueId == boutiqueId && s.SaleDate >= start && s.SaleDate < endExclusive)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            var expenses = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.BoutiqueId == boutiqueId && e.Date >= start && e.Date < endExclusive)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            var storeName = await _context.StoreSettings
                .AsNoTracking()
                .Where(s => s.BoutiqueId == boutiqueId)
                .Select(s => s.StoreName)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(storeName))
            {
                storeName = await _context.Boutiques
                    .AsNoTracking()
                    .Where(b => b.Id == boutiqueId)
                    .Select(b => b.Nom)
                    .FirstOrDefaultAsync() ?? "Ma Boutique";
            }

            // endExclusive.AddTicks(-1) couvre toute la journée de fin
            var grossProfit = await _profitService.CalculateGrossProfitAsync(boutiqueId, start, endExclusive.AddTicks(-1));

            var byPayment = sales
                .GroupBy(s => string.IsNullOrWhiteSpace(s.PaymentMethod) ? "Cash" : s.PaymentMethod!)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.FinalPrice));

            var byCategory = expenses
                .GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? "Autre" : e.Category)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            return new ReportViewModel
            {
                StoreName = storeName,
                StartDate = start,
                EndDate = end,
                Sales = sales,
                Expenses = expenses,
                TotalSales = sales.Sum(s => s.FinalPrice),
                GrossProfit = grossProfit,
                TotalExpenses = expenses.Sum(e => e.Amount),
                SalesByPaymentMethod = byPayment,
                ExpensesByCategory = byCategory
            };
        }
    }
}
