using AdGestionHub.Data;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBoutiqueService _boutiqueService;
        private readonly ICacheService _cacheService;

        public ProductsController(
            ApplicationDbContext context,
            IBoutiqueService boutiqueService,
            ICacheService cacheService)
        {
            _context = context;
            _boutiqueService = boutiqueService;
            _cacheService = cacheService;
        }

        // GET: Products (accessible aux Admin et Employés de la boutique)
        public async Task<IActionResult> Index(string? search, string? status)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var query = _context.Products
                .AsNoTracking()
                .Where(p => p.BoutiqueId == boutiqueId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p => p.Name.Contains(term) || (p.Variations != null && p.Variations.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                switch (status.ToLowerInvariant())
                {
                    case "out":
                        query = query.Where(p => p.StockQuantity <= 0);
                        break;
                    case "low":
                        query = query.Where(p => p.StockQuantity > 0 && p.StockQuantity <= (p.LowStockThreshold > 0 ? p.LowStockThreshold : p.StockAlertThreshold));
                        break;
                    case "ok":
                        query = query.Where(p => p.StockQuantity > (p.LowStockThreshold > 0 ? p.LowStockThreshold : p.StockAlertThreshold));
                        break;
                }
            }

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;

            var products = await query
                .OrderBy(p => p.Name)
                .ToListAsync();

            return View(products);
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.BoutiqueId == boutiqueId.Value && p.Id == id.Value);

            if (product == null) return NotFound();
            return View(product);
        }

        // GET: Products/Create
        [Authorize(Policy = "AdminOrSuperAdmin")]
        public IActionResult Create()
        {
            return View(new Product { StockAlertThreshold = 5, LowStockThreshold = 5 });
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrSuperAdmin")]
        public async Task<IActionResult> Create(ProductFormViewModel model)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            if (!ModelState.IsValid)
            {
                return View(MapToEntity(model, boutiqueId.Value));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var product = new Product
            {
                Name = model.Name.Trim(),
                PurchasePrice = decimal.Round(model.PurchasePrice, 2),
                SalePrice = decimal.Round(model.SalePrice, 2),
                StockQuantity = model.StockQuantity,
                LowStockThreshold = model.StockAlertThreshold,
                StockAlertThreshold = model.StockAlertThreshold,
                Variations = string.IsNullOrWhiteSpace(model.Variations) ? null : model.Variations.Trim(),
                BoutiqueId = boutiqueId.Value,
                UserId = userId
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            await _cacheService.RemoveAsync($"Dashboard_{boutiqueId.Value}");

            TempData["SuccessMessage"] = $"Le produit « {product.Name} » a été ajouté avec succès.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Products/Edit/5
        [Authorize(Policy = "AdminOrSuperAdmin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.BoutiqueId == boutiqueId.Value && p.Id == id.Value);

            if (product == null) return NotFound();

            if (product.StockAlertThreshold <= 0 && product.LowStockThreshold > 0)
                product.StockAlertThreshold = product.LowStockThreshold;

            return View(product);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrSuperAdmin")]
        public async Task<IActionResult> Edit(int id, ProductFormViewModel model)
        {
            if (id != model.Id) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var existing = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.BoutiqueId == boutiqueId.Value);

            if (existing == null) return NotFound();

            if (!ModelState.IsValid)
            {
                return View(MapToEntity(model, boutiqueId.Value));
            }

            existing.Name = model.Name.Trim();
            existing.PurchasePrice = decimal.Round(model.PurchasePrice, 2);
            existing.SalePrice = decimal.Round(model.SalePrice, 2);
            existing.StockQuantity = model.StockQuantity;
            existing.LowStockThreshold = model.StockAlertThreshold;
            existing.StockAlertThreshold = model.StockAlertThreshold;
            existing.Variations = string.IsNullOrWhiteSpace(model.Variations) ? null : model.Variations.Trim();

            await _context.SaveChangesAsync();
            await _cacheService.RemoveAsync($"Dashboard_{boutiqueId.Value}");

            TempData["SuccessMessage"] = $"Le produit « {existing.Name} » a été mis à jour.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Products/Delete/5
        [Authorize(Policy = "AdminOrSuperAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.BoutiqueId == boutiqueId.Value && p.Id == id.Value);

            if (product == null) return NotFound();
            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrSuperAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.BoutiqueId == boutiqueId.Value && p.Id == id);

            if (product != null)
            {
                // Détacher les lignes de ventes passées (le nom et le prix sont déjà conservés sur SaleItem)
                await _context.SaleItems
                    .Where(si => si.BoutiqueId == boutiqueId.Value && si.ProductId == product.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(si => si.ProductId, (int?)null));

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                await _cacheService.RemoveAsync($"Dashboard_{boutiqueId.Value}");
                TempData["SuccessMessage"] = "Produit supprimé de l'inventaire.";
            }

            return RedirectToAction(nameof(Index));
        }

        private static Product MapToEntity(ProductFormViewModel model, int boutiqueId) => new()
        {
            Id = model.Id,
            Name = model.Name,
            PurchasePrice = model.PurchasePrice,
            SalePrice = model.SalePrice,
            StockQuantity = model.StockQuantity,
            LowStockThreshold = model.StockAlertThreshold,
            StockAlertThreshold = model.StockAlertThreshold,
            Variations = model.Variations,
            BoutiqueId = boutiqueId
        };
    }
}
