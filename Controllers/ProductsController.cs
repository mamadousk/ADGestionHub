using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICacheService cacheService,
            ILogger<ProductsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _cacheService = cacheService;
            _logger = logger;
        }

        // ========== INDEX ==========
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var cacheKey = $"Products_List_{user.BoutiqueId ?? 0}";

            var products = await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                IQueryable<Product> query = _context.Products.AsNoTracking();
                if (!User.IsInRole("SuperAdmin"))
                    query = query.Where(p => p.BoutiqueId == user.BoutiqueId);
                return await query.ToListAsync();
            }, TimeSpan.FromMinutes(5));

            return View(products);
        }

        // ========== DETAILS ==========
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.BoutiqueId == user.BoutiqueId);

            if (product == null) return NotFound();

            return View(product);
        }

        // ========== CREATE (GET) ==========
        public IActionResult Create() => View();

        // ========== CREATE (POST) ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,PurchasePrice,SalePrice,StockQuantity,LowStockThreshold,StockAlertThreshold,Variations")] Product product)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (ModelState.IsValid)
            {
                product.BoutiqueId = user.BoutiqueId;
                product.UserId = user.Id;
                _context.Add(product);
                await _context.SaveChangesAsync();

                await _cacheService.RemoveAsync($"Products_List_{user.BoutiqueId ?? 0}");
                TempData["SuccessMessage"] = "Produit créé avec succès !";
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        // ========== EDIT (GET) ==========
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.BoutiqueId == user.BoutiqueId);

            if (product == null) return NotFound();

            return View(product);
        }

        // ========== EDIT (POST) ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,PurchasePrice,SalePrice,StockQuantity,LowStockThreshold,StockAlertThreshold,Variations")] Product product)
        {
            if (id != product.Id) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var existingProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.BoutiqueId == user.BoutiqueId);

            if (existingProduct == null) return NotFound();

            if (!ModelState.IsValid)
                return View(product);

            try
            {
                existingProduct.Name = product.Name;
                existingProduct.PurchasePrice = product.PurchasePrice;
                existingProduct.SalePrice = product.SalePrice;
                existingProduct.StockQuantity = product.StockQuantity;
                existingProduct.LowStockThreshold = product.LowStockThreshold;
                existingProduct.StockAlertThreshold = product.StockAlertThreshold;
                existingProduct.Variations = product.Variations;

                _context.Update(existingProduct);
                await _context.SaveChangesAsync();

                await _cacheService.RemoveAsync($"Products_List_{user.BoutiqueId ?? 0}");
                await _cacheService.RemoveAsync($"Product_Details_{id}_{user.BoutiqueId ?? 0}");

                TempData["SuccessMessage"] = "Produit mis à jour avec succès !";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await ProductExists(id)) return NotFound();
                else throw;
            }
        }

        // ========== DELETE ==========
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.BoutiqueId == user.BoutiqueId);

            if (product == null) return NotFound();

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.BoutiqueId == user.BoutiqueId);

            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                await _cacheService.RemoveAsync($"Products_List_{user.BoutiqueId ?? 0}");
                await _cacheService.RemoveAsync($"Product_Details_{id}_{user.BoutiqueId ?? 0}");

                TempData["SuccessMessage"] = "Produit supprimé avec succès !";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> ProductExists(int id)
        {
            return await _context.Products.AnyAsync(e => e.Id == id);
        }
    }
}