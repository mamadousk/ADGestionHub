using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
            if (user == null) return Challenge();

            var sales = await _context.Sales
                .Include(s => s.Items)
                .Where(s => s.BoutiqueId == user.BoutiqueId)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            var products = await _context.Products
                .Where(p => p.BoutiqueId == user.BoutiqueId)
                .ToListAsync();

            var expenses = await _context.Expenses
                .Where(e => e.BoutiqueId == user.BoutiqueId)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            var debts = await _context.Debts
                .Where(d => d.BoutiqueId == user.BoutiqueId)
                .ToListAsync();

            decimal totalSales = sales.Sum(s => s.FinalPrice);
            decimal totalExpenses = expenses.Sum(e => e.Amount);

            // CALCUL DU PROFIT RÉEL
            decimal grossProfit = 0;
            foreach (var sale in sales)
            {
                foreach (var item in sale.Items)
                {
                    var product = products.FirstOrDefault(p => p.Id == item.ProductId);

                    // Sécurité : si le produit existe, on déduit le prix d'achat. 
                    // Si c'est une saisie libre (product == null), on considère la marge sur le prix total.
                    decimal purchaseCost = (product?.PurchasePrice ?? 0) * item.Quantity;
                    grossProfit += (item.UnitPrice * item.Quantity) - purchaseCost;
                }
            }

            ViewBag.TotalDebts = debts.Sum(d => d.Amount);

            var model = new DashboardViewModel
            {
                TotalSales = totalSales,
                TotalExpenses = totalExpenses,
                TotalProfit = grossProfit - totalExpenses,
                TotalTransactions = sales.Count,
                LowStockCount = products.Count(p => p.StockQuantity <= p.LowStockThreshold),
                RecentSales = sales.Take(5).ToList(),
                RecentExpenses = expenses.Take(5).ToList()
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