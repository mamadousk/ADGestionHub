using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;

namespace AdGestionHub.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> GenerateDailyReport(DateTime? date)
        {
            var user = await _userManager.GetUserAsync(User);
            DateTime reportDate = date ?? DateTime.Today;

            var sales = await _context.Sales
                .Include(s => s.Items)
                .Where(s => s.BoutiqueId == user.BoutiqueId && s.SaleDate.Date == reportDate.Date)
                .ToListAsync();

            var expenses = await _context.Expenses
                .Where(e => e.BoutiqueId == user.BoutiqueId && e.Date.Date == reportDate.Date)
                .ToListAsync();

            ViewData["ReportDate"] = reportDate.ToString("dd/MM/yyyy");
            ViewData["StoreName"] = _context.StoreSettings
                .FirstOrDefault(s => s.BoutiqueId == user.BoutiqueId)?.StoreName ?? "Ma Boutique";

            var model = new ReportViewModel
            {
                Sales = sales,
                Expenses = expenses,
                TotalSales = sales.Sum(s => s.FinalPrice),
                TotalExpenses = expenses.Sum(e => e.Amount)
            };

            return new ViewAsPdf("DailyReportPdf", model)
            {
                FileName = $"Rapport_{reportDate:yyyyMMdd}.pdf",
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }
    }
}