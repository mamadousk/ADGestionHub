using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
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
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(nameof(Index));
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

            var model = await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var currentMonthStart = new DateTime(today.Year, today.Month, 1);
                var prevMonthStart = currentMonthStart.AddMonths(-1);

                var totalSales = await _context.Sales
                    .AsNoTracking()
                    .Where(s => s.BoutiqueId == boutiqueId)
                    .SumAsync(s => s.FinalPrice);

                var totalTransactions = await _context.Sales
                    .AsNoTracking()
                    .CountAsync(s => s.BoutiqueId == boutiqueId);

                var todaySales = await _context.Sales
                    .AsNoTracking()
                    .Where(s => s.BoutiqueId == boutiqueId && s.SaleDate >= today && s.SaleDate < tomorrow)
                    .SumAsync(s => s.FinalPrice);

                var todayTransactions = await _context.Sales
                    .AsNoTracking()
                    .CountAsync(s => s.BoutiqueId == boutiqueId && s.SaleDate >= today && s.SaleDate < tomorrow);

                var currentMonthSales = await _context.Sales
                    .AsNoTracking()
                    .Where(s => s.BoutiqueId == boutiqueId && s.SaleDate >= currentMonthStart)
                    .SumAsync(s => s.FinalPrice);

                var prevMonthSales = await _context.Sales
                    .AsNoTracking()
                    .Where(s => s.BoutiqueId == boutiqueId && s.SaleDate >= prevMonthStart && s.SaleDate < currentMonthStart)
                    .SumAsync(s => s.FinalPrice);

                decimal? monthlyGrowth = prevMonthSales > 0m
                    ? Math.Round((currentMonthSales - prevMonthSales) / prevMonthSales * 100m, 1)
                    : null;

                var totalExpenses = await _context.Expenses
                    .AsNoTracking()
                    .Where(e => e.BoutiqueId == boutiqueId)
                    .SumAsync(e => e.Amount);

                var totalDebts = await _context.Debts
                    .AsNoTracking()
                    .Where(d => d.BoutiqueId == boutiqueId && !d.IsArchived && !d.IsPaid)
                    .SumAsync(d => d.Amount);

                var grossProfit = await _profitService.CalculateGrossProfitAsync(boutiqueId);
                var netProfit = grossProfit - totalExpenses;
                var lowStockCount = await _stockService.GetLowStockCountAsync(boutiqueId);

                var recentSales = await _context.Sales
                    .AsNoTracking()
                    .Include(s => s.Items)
                    .Where(s => s.BoutiqueId == boutiqueId)
                    .OrderByDescending(s => s.SaleDate)
                    .Take(5)
                    .ToListAsync();

                var recentExpenses = await _context.Expenses
                    .AsNoTracking()
                    .Where(e => e.BoutiqueId == boutiqueId)
                    .OrderByDescending(e => e.Date)
                    .Take(5)
                    .ToListAsync();

                var lowStockProducts = await _context.Products
                    .AsNoTracking()
                    .Where(p => p.BoutiqueId == boutiqueId
                             && p.StockQuantity <= (p.LowStockThreshold > 0 ? p.LowStockThreshold : p.StockAlertThreshold))
                    .OrderBy(p => p.StockQuantity)
                    .Take(5)
                    .ToListAsync();

                return new DashboardViewModel
                {
                    TotalSales = totalSales,
                    TodaySales = todaySales,
                    TodayTransactions = todayTransactions,
                    TotalExpenses = totalExpenses,
                    TotalProfit = netProfit,
                    TotalDebts = totalDebts,
                    MonthlyGrowthPercent = monthlyGrowth,
                    TotalTransactions = totalTransactions,
                    LowStockCount = lowStockCount,
                    RecentSales = recentSales,
                    RecentExpenses = recentExpenses,
                    LowStockProducts = lowStockProducts
                };
            }, TimeSpan.FromMinutes(2));

            ViewBag.TotalDebts = model.TotalDebts;
            return View(model);
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
