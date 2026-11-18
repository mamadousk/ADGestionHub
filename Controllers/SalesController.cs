using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

[Authorize(Policy = "ShopMember")]
public sealed class SalesController(ApplicationDbContext db, IBoutiqueService boutiques, ITenantContextService tenant,
    CommerceOperations commerce, IPdfReportService pdf) : TradeController(db, boutiques, tenant)
{
    public async Task<IActionResult> Index()
    {
        if (await ShopAsync() is not int shop) return Forbid();
        return View(await db.Sales.AsNoTracking().Where(s => s.BoutiqueId == shop).Include(s => s.Payments)
            .OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.Id).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? productId)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        await PrepareAsync(shop);
        return View(new SaleInput
        {
            CurrencyCode = (await tenant.GetContextAsync()).PrimaryCurrency.Code,
            Items = [new TradeLineInput { ProductId = productId ?? 0 }]
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaleInput model)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                var sale = await commerce.SellAsync(shop, model, User.IsInRole("Admin"));
                TempData["SuccessMessage"] = $"Vente {sale.Number} enregistrée. Le stock a été mis à jour.";
                if (sale.StockWarnings > 0)
                    TempData["StockNotice"] = $"{sale.StockWarnings} produit(s) de cette vente sont au seuil d’alerte ou en dessous.";
                return RedirectToAction(nameof(Details), new { id = sale.Id });
            }
            catch (CommerceRuleException error) { ModelState.AddModelError("", error.Message); }
            catch (CommerceNotFoundException) { return NotFound(); }
        }
        await PrepareAsync(shop);
        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        var sale = await FindAsync(shop, id);
        return sale == null ? NotFound() : View(sale);
    }

    public async Task<IActionResult> Receipt(int id, string? format = null)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        var sale = await FindAsync(shop, id);
        if (sale == null) return NotFound();
        var settings = (await tenant.GetContextAsync()).Settings;
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            return File(pdf.SalePdf(sale, settings), "application/pdf", $"vente-{sale.Id}.pdf");
        ViewData["StoreSettings"] = settings;
        return View(sale);
    }

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        var sale = await FindAsync(shop, id);
        return sale == null ? NotFound() : View(sale);
    }

    [HttpPost, ActionName("Delete"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        try
        {
            if (await commerce.CancelSaleAsync(shop, id, reason))
                TempData["SuccessMessage"] = "Vente annulée. Les quantités ont été remises en stock.";
        }
        catch (CommerceRuleException error) { TempData["ErrorMessage"] = error.Message; }
        catch (CommerceNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Details), new { id });
    }

    private Task<Sale?> FindAsync(int shop, int id) => db.Sales.AsNoTracking().Include(s => s.Items.Where(i => i.BoutiqueId == shop))
        .Include(s => s.Payments).SingleOrDefaultAsync(s => s.BoutiqueId == shop && s.Id == id);
}
