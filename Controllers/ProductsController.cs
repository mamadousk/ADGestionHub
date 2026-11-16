using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AdGestionHub.Data;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBoutiqueService _boutiqueService;
        private readonly ITenantContextService _tenantContext;
        private readonly ICacheService _cacheService;
        private readonly IAppLocalizer _L;

        public ProductsController(
            ApplicationDbContext context,
            IBoutiqueService boutiqueService,
            ITenantContextService tenantContext,
            ICacheService cacheService,
            IAppLocalizer localizer)
        {
            _context = context;
            _boutiqueService = boutiqueService;
            _tenantContext = tenantContext;
            _cacheService = cacheService;
            _L = localizer;
        }

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
                query = query.Where(p =>
                    p.Name.Contains(term)
                    || (p.Sku != null && p.Sku.Contains(term))
                    || (p.Variations != null && p.Variations.Contains(term)));
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

            var products = await query.OrderBy(p => p.Name).ToListAsync();
            return View(products);
        }

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

        [Authorize(Policy = "AdminOrSuperAdmin")]
        public async Task<IActionResult> Create()
        {
            var ctx = await _tenantContext.GetContextAsync();
            return View(new Product
            {
                StockAlertThreshold = 5,
                LowStockThreshold = 5,
                VatRate = ctx.IsVatEnabled ? ctx.DefaultVatRate : null
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrSuperAdmin")]
        public async Task<IActionResult> Create(ProductFormViewModel model)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var ctx = await _tenantContext.GetContextAsync();
            var decimals = ctx.PrimaryCurrency.Decimals;

            if (!ModelState.IsValid)
                return View(MapToEntity(model, boutiqueId.Value));

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var product = new Product
            {
                Name = model.Name.Trim(),
                Sku = string.IsNullOrWhiteSpace(model.Sku) ? null : model.Sku.Trim(),
                PurchasePrice = decimal.Round(model.PurchasePrice, decimals),
                SalePrice = decimal.Round(model.SalePrice, decimals),
                VatRate = ctx.IsVatEnabled ? (model.VatRate ?? ctx.DefaultVatRate) : null,
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

            TempData["SuccessMessage"] = _L.T(
                $"Produit « {product.Name} » ajouté au catalogue.",
                $"Product « {product.Name} » added to catalog.");
            return RedirectToAction(nameof(Index));
        }

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

            var ctx = await _tenantContext.GetContextAsync();
            if (ctx.IsVatEnabled && !product.VatRate.HasValue)
                product.VatRate = ctx.DefaultVatRate;

            return View(product);
        }

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
                return View(MapToEntity(model, boutiqueId.Value));

            var ctx = await _tenantContext.GetContextAsync();
            var decimals = ctx.PrimaryCurrency.Decimals;

            existing.Name = model.Name.Trim();
            existing.Sku = string.IsNullOrWhiteSpace(model.Sku) ? null : model.Sku.Trim();
            existing.PurchasePrice = decimal.Round(model.PurchasePrice, decimals);
            existing.SalePrice = decimal.Round(model.SalePrice, decimals);
            if (ctx.IsVatEnabled)
                existing.VatRate = model.VatRate ?? ctx.DefaultVatRate;

            existing.StockQuantity = model.StockQuantity;
            existing.LowStockThreshold = model.StockAlertThreshold;
            existing.StockAlertThreshold = model.StockAlertThreshold;
            existing.Variations = string.IsNullOrWhiteSpace(model.Variations) ? null : model.Variations.Trim();

            await _context.SaveChangesAsync();
            await _cacheService.RemoveAsync($"Dashboard_{boutiqueId.Value}");

            TempData["SuccessMessage"] = _L.T(
                $"Produit « {existing.Name} » mis à jour.",
                $"Product « {existing.Name} » updated.");
            return RedirectToAction(nameof(Index));
        }

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
                await _context.SaleItems
                    .Where(si => si.BoutiqueId == boutiqueId.Value && si.ProductId == product.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(si => si.ProductId, (int?)null));

                await _context.QuoteItems
                    .Where(qi => qi.BoutiqueId == boutiqueId.Value && qi.ProductId == product.Id)
                    .ExecuteUpdateAsync(q => q.SetProperty(qi => qi.ProductId, (int?)null));

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                await _cacheService.RemoveAsync($"Dashboard_{boutiqueId.Value}");
                TempData["SuccessMessage"] = _L.T("Produit supprimé du catalogue.", "Product removed from catalog.");
            }

            return RedirectToAction(nameof(Index));
        }

        private static Product MapToEntity(ProductFormViewModel model, int boutiqueId) => new()
        {
            Id = model.Id,
            Name = model.Name,
            Sku = model.Sku,
            PurchasePrice = model.PurchasePrice,
            SalePrice = model.SalePrice,
            VatRate = model.VatRate,
            StockQuantity = model.StockQuantity,
            LowStockThreshold = model.StockAlertThreshold,
            StockAlertThreshold = model.StockAlertThreshold,
            Variations = model.Variations,
            BoutiqueId = boutiqueId
        };
    }
}
