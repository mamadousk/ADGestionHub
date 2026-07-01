// ==================== FILE: Controllers/HomeController.cs ====================
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProfitService _profitService;
        private readonly IStockService _stockService;
        private readonly ICacheService _cacheService;

        public HomeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IProfitService profitService,
            IStockService stockService,
            ICacheService cacheService)
        {
            _context = context;
            _userManager = userManager;
            _profitService = profitService;
            _stockService = stockService;
            _cacheService = cacheService;
        }

        [AllowAnonymous]
        public IActionResult Welcome()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index");
            }
            return View();
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("SuperAdmin"))
            {
                return RedirectToAction("Index", "SuperAdmin");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var cacheKey = $"Dashboard_{boutiqueId}";

            // Tentative de récupérer les données depuis le cache
            var cachedData = await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                // Requêtes optimisées avec AsNoTracking()
                var sales = await _context.Sales
                    .AsNoTracking()
                    .Include(s => s.Items)
                    .Where(s => s.BoutiqueId == boutiqueId)
                    .OrderByDescending(s => s.SaleDate)
                    .ToListAsync();

                var products = await _context.Products
                    .AsNoTracking()
                    .Where(p => p.BoutiqueId == boutiqueId)
                    .ToListAsync();

                var expenses = await _context.Expenses
                    .AsNoTracking()
                    .Where(e => e.BoutiqueId == boutiqueId)
                    .OrderByDescending(e => e.Date)
                    .ToListAsync();

                var debts = await _context.Debts
                    .AsNoTracking()
                    .Where(d => d.BoutiqueId == boutiqueId && !d.IsArchived)
                    .ToListAsync();

                var totalSales = sales.Sum(s => s.FinalPrice);
                var totalExpenses = expenses.Sum(e => e.Amount);
                var grossProfit = await _profitService.CalculateGrossProfitAsync(boutiqueId);
                var netProfit = grossProfit - totalExpenses;
                var lowStockCount = await _stockService.GetLowStockCountAsync(boutiqueId);

                return new
                {
                    TotalSales = totalSales,
                    TotalExpenses = totalExpenses,
                    TotalProfit = netProfit,
                    TotalTransactions = sales.Count,
                    LowStockCount = lowStockCount,
                    RecentSales = sales.Take(5).ToList(),
                    RecentExpenses = expenses.Take(5).ToList(),
                    TotalDebts = debts.Sum(d => d.Amount)
                };
            }, TimeSpan.FromMinutes(2)); // Cache de 2 minutes

            ViewBag.TotalDebts = cachedData.TotalDebts;

            var model = new DashboardViewModel
            {
                TotalSales = cachedData.TotalSales,
                TotalExpenses = cachedData.TotalExpenses,
                TotalProfit = cachedData.TotalProfit,
                TotalTransactions = cachedData.TotalTransactions,
                LowStockCount = cachedData.LowStockCount,
                RecentSales = cachedData.RecentSales,
                RecentExpenses = cachedData.RecentExpenses
            };

            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}