using System;
using System.Linq;
using System.Threading.Tasks;
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantContextService _tenantContext;
        private readonly IPdfReportService _pdfReportService;
        private readonly IAppLocalizer _L;

        public ReportsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITenantContextService tenantContext,
            IPdfReportService pdfReportService,
            IAppLocalizer localizer)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
            _pdfReportService = pdfReportService;
            _L = localizer;
        }

        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string? taxFilter = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
            {
                if (User.IsInRole("SuperAdmin"))
                    return RedirectToAction("Index", "SuperAdmin");
                return Challenge();
            }

            var model = await BuildReportAsync(user.BoutiqueId.Value, startDate, endDate, taxFilter);
            return View(model);
        }

        public async Task<IActionResult> GenerateDailyReport(DateTime? date, DateTime? startDate, DateTime? endDate, string? taxFilter = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var effectiveStart = startDate ?? date ?? DateTime.Today;
            var effectiveEnd = endDate ?? date ?? effectiveStart;

            var model = await BuildReportAsync(user.BoutiqueId.Value, effectiveStart, effectiveEnd, taxFilter);
            var pdfBytes = _pdfReportService.GeneratePeriodReportPdf(model, _L.CurrentLanguage);

            var fileName = model.StartDate.Date == model.EndDate.Date
                ? $"Rapport_{model.StartDate:yyyyMMdd}.pdf"
                : $"Rapport_{model.StartDate:yyyyMMdd}_{model.EndDate:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        private async Task<ReportViewModel> BuildReportAsync(int boutiqueId, DateTime? startDate, DateTime? endDate, string? taxFilter)
        {
            var ctx = await _tenantContext.GetContextAsync();
            var start = (startDate ?? DateTime.Today).Date;
            var end = (endDate ?? start).Date;
            if (end < start)
                (start, end) = (end, start);

            var endExclusive = end.AddDays(1);

            var salesQuery = _context.Sales
                .AsNoTracking()
                .Include(s => s.Items)
                .Where(s => s.BoutiqueId == boutiqueId && !s.IsCancelled && s.SaleDate >= start && s.SaleDate < endExclusive);

            if (ctx.IsVatEnabled && !string.IsNullOrWhiteSpace(taxFilter))
            {
                if (string.Equals(taxFilter, "taxed", StringComparison.OrdinalIgnoreCase))
                    salesQuery = salesQuery.Where(s => s.TaxAmount > 0);
                else if (string.Equals(taxFilter, "exempt", StringComparison.OrdinalIgnoreCase))
                    salesQuery = salesQuery.Where(s => s.TaxAmount == 0);
            }

            var allPeriodSales = await salesQuery
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            var expenses = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.BoutiqueId == boutiqueId && e.Date >= start && e.Date < endExclusive)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            var primaryCode = ctx.PrimaryCurrency.Code;

            // Pour éviter d'additionner des devises différentes si la boutique a changé de devise au fil du temps,
            // les totaux KPI agrègent la devise principale active, tandis que SalesByCurrency ventile chaque devise historique.
            var primarySales = allPeriodSales
                .Where(s => string.Equals(s.CurrencyCode, primaryCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var primaryExpenses = expenses
                .Where(e => string.Equals(e.CurrencyCode, primaryCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Si aucune vente n'est dans la nouvelle devise mais qu'il existe des ventes historiques dans une seule devise, afficher cette devise
            var kpiCurrency = ctx.PrimaryCurrency;
            if (!primarySales.Any() && allPeriodSales.Any())
            {
                var distinctCodes = allPeriodSales.Select(s => s.CurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (distinctCodes.Count == 1)
                {
                    kpiCurrency = CountryProfileCatalog.GetCurrency(distinctCodes[0]);
                    primarySales = allPeriodSales;
                    primaryExpenses = expenses
                        .Where(e => string.Equals(e.CurrencyCode, kpiCurrency.Code, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
            }

            var grossProfit = primarySales
                .SelectMany(s => s.Items)
                .Sum(si => (si.UnitPrice - si.UnitCost) * si.Quantity);

            var byPayment = primarySales
                .GroupBy(s => string.IsNullOrWhiteSpace(s.PaymentMethod) ? "Cash" : s.PaymentMethod!)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.FinalPrice));

            var byCurrency = allPeriodSales
                .GroupBy(s => string.IsNullOrWhiteSpace(s.CurrencyCode) ? "USD" : s.CurrencyCode.ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.Sum(x => x.FinalPrice));

            var byCategory = primaryExpenses
                .GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? "Autre" : e.Category)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            return new ReportViewModel
            {
                StoreName = ctx.Settings.StoreName,
                CurrencyCode = kpiCurrency.Code,
                CurrencySymbol = kpiCurrency.Symbol,
                CurrencyDecimals = kpiCurrency.Decimals,
                IsVatEnabled = ctx.IsVatEnabled,
                IsFormalized = ctx.IsFormalized,
                TaxFilter = taxFilter,
                StartDate = start,
                EndDate = end,
                Sales = allPeriodSales,
                Expenses = expenses,
                TotalSales = primarySales.Sum(s => s.FinalPrice),
                TotalSubtotalNet = primarySales.Sum(s => s.EffectiveSubtotalNet),
                TotalTaxAmount = primarySales.Sum(s => s.TaxAmount),
                GrossProfit = grossProfit,
                TotalExpenses = primaryExpenses.Sum(e => e.Amount),
                SalesByPaymentMethod = byPayment,
                SalesByCurrency = byCurrency,
                ExpensesByCategory = byCategory
            };
        }
    }
}
