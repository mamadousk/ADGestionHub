using System.Security.Claims;
using AdGestionHub.Data;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

[Authorize(Policy = StaffAccessRules.Catalogue)]
public sealed class ProductsController(ApplicationDbContext db, IBoutiqueService boutiques, ITenantContextService tenant,
    CommerceWriter writer, StockService stock) : Controller
{
    public async Task<IActionResult> Index(string? search, string? status)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var query = Products(shop);
        if (CommerceOperations.Clean(search) is string term)
            query = query.Where(p => p.Name.Contains(term) || (p.Sku != null && p.Sku.Contains(term)) || (p.Category != null && p.Category.Contains(term)));
        query = status switch
        {
            "archived" => query.Where(p => p.IsArchived),
            "out" => query.Where(p => !p.IsArchived && p.StockQuantity == 0),
            "low" => query.Where(p => !p.IsArchived && p.StockQuantity <= p.StockAlertThreshold),
            "ok" => query.Where(p => !p.IsArchived && p.StockQuantity > p.StockAlertThreshold),
            _ => query.Where(p => !p.IsArchived)
        };
        ViewData["Search"] = search;
        ViewData["Status"] = status;
        return View(await query.AsNoTracking().OrderBy(p => p.Name).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var product = await Products(shop).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();
        ViewData["StockMovements"] = await db.StockMovements.AsNoTracking().Where(m => m.BoutiqueId == shop && m.ProductId == id)
            .OrderByDescending(m => m.RecordedAt).ThenByDescending(m => m.Id).ToListAsync();
        return View(product);
    }

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create() => View("Form", new ProductFormViewModel
    {
        CatalogCurrencyCode = (await tenant.GetContextAsync()).PrimaryCurrency.Code
    });

    [HttpPost, Authorize(Roles = "Admin")]
    public Task<IActionResult> Create(ProductFormViewModel model) => SaveAsync(null, model);

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var product = await Products(shop).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && !p.IsArchived);
        if (product == null) return NotFound();
        ViewData["ProductId"] = id;
        return View("Form", new ProductFormViewModel
        {
            Name = product.Name, Sku = product.Sku, Category = product.Category, Description = product.Description,
            PurchasePrice = product.PurchasePrice, SalePrice = product.SalePrice, StockAlertThreshold = product.StockAlertThreshold,
            CatalogCurrencyCode = (await tenant.GetContextAsync()).PrimaryCurrency.Code
        });
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public Task<IActionResult> Edit(int id, ProductFormViewModel model) => SaveAsync(id, model);

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Adjust(int id, StockAdjustmentInput model)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Saisissez une quantité entre 1 et 100 000 et un motif (250 caractères maximum).";
            return RedirectToAction(nameof(Details), new { id });
        }
        try
        {
            await writer.ExecuteAsync(shop, async () =>
            {
                var product = await Products(shop).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && !p.IsArchived)
                    ?? throw new CommerceNotFoundException();
                var delta = model.IsEntry ? model.Quantity : -model.Quantity;
                await stock.ApplyAsync(shop, product, delta, "Ajustement manuel", model.Reason.Trim(), DateTime.Now);
                writer.Audit(shop, $"Ajustement stock du produit #{id} : {delta:+0;-0}", model.Reason.Trim());
                return true;
            });
            TempData["SuccessMessage"] = "Mouvement de stock enregistré.";
        }
        catch (CommerceRuleException error) { TempData["ErrorMessage"] = error.Message; }
        catch (CommerceNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        try
        {
            await writer.ExecuteAsync(shop, async () =>
            {
                var product = await Products(shop).SingleOrDefaultAsync(p => p.Id == id) ?? throw new CommerceNotFoundException();
                if (product.StockQuantity != 0) throw new CommerceRuleException("Ramenez le stock à zéro avant d'archiver cet article.");
                product.IsArchived = true;
                writer.Audit(shop, $"Archivage du produit #{id}", product.Name);
                return true;
            });
            TempData["SuccessMessage"] = "Produit retiré du catalogue. Son historique reste consultable.";
            return RedirectToAction(nameof(Index));
        }
        catch (CommerceRuleException error) { TempData["ErrorMessage"] = error.Message; }
        catch (CommerceNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Restore(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        try
        {
            await writer.ExecuteAsync(shop, async () =>
            {
                var product = await Products(shop).SingleOrDefaultAsync(p => p.Id == id) ?? throw new CommerceNotFoundException();
                product.IsArchived = false;
                writer.Audit(shop, $"Réactivation du produit #{id}", product.Name);
                return true;
            });
            TempData["SuccessMessage"] = "Produit réactivé.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (CommerceNotFoundException) { return NotFound(); }
    }

    private IQueryable<Product> Products(int shop) => db.Products.Where(p => p.BoutiqueId == shop);

    private async Task<IActionResult> SaveAsync(int? id, ProductFormViewModel model)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                var productId = await writer.ExecuteAsync(shop, async () =>
                {
                    var settings = await writer.SettingsAsync(shop);
                    var code = settings.PrimaryCurrencyCode;
                    if (code != model.CatalogCurrencyCode)
                        throw new CommerceRuleException("La devise du catalogue a changé. Vérifiez les prix avant de réessayer.");
                    if (!Money.IsMvpCurrency(code)) throw new CommerceRuleException("Le catalogue V1 est disponible en CDF et en USD.");
                    var product = !id.HasValue ? new Product { BoutiqueId = shop, UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "" }
                        : await Products(shop).SingleOrDefaultAsync(p => p.Id == id && !p.IsArchived) ?? throw new CommerceNotFoundException();
                    product.Name = model.Name.Trim();
                    product.Sku = CommerceOperations.Clean(model.Sku);
                    product.Category = CommerceOperations.Clean(model.Category);
                    product.Description = CommerceOperations.Clean(model.Description);
                    product.PurchasePrice = Money.Amount(model.PurchasePrice, code, allowZero: true);
                    product.SalePrice = Money.Amount(model.SalePrice, code, allowZero: true);
                    product.StockAlertThreshold = product.LowStockThreshold = model.StockAlertThreshold;
                    if (!id.HasValue) db.Products.Add(product);
                    await db.SaveChangesAsync();
                    if (!id.HasValue && model.StockQuantity > 0)
                        await stock.ApplyAsync(shop, product, model.StockQuantity, "Stock initial", null, DateTime.Now);
                    writer.Audit(shop, $"Catalogue : produit #{product.Id} {(id.HasValue ? "modifié" : "ajouté")}", product.Name);
                    return product.Id;
                });
                TempData["SuccessMessage"] = id.HasValue ? "Produit mis à jour." : "Produit ajouté. Vous pouvez maintenant enregistrer une vente.";
                return RedirectToAction(nameof(Details), new { id = productId });
            }
            catch (CommerceRuleException error) { ModelState.AddModelError("", error.Message); }
            catch (CommerceNotFoundException) { return NotFound(); }
        }
        ViewData["ProductId"] = id;
        ModelState.Remove(nameof(model.CatalogCurrencyCode));
        model.CatalogCurrencyCode = (await tenant.GetContextAsync()).PrimaryCurrency.Code;
        return View("Form", model);
    }
}
