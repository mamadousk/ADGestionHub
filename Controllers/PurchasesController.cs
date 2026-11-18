using AdGestionHub.Data;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

[Authorize(Roles = "Admin")]
public sealed class PurchasesController(ApplicationDbContext db, IBoutiqueService boutiques, ITenantContextService tenant,
    CommerceOperations commerce) : TradeController(db, boutiques, tenant)
{
    public async Task<IActionResult> Index()
    {
        if (await ShopAsync() is not int shop) return Forbid();
        return View(await Db.Purchases.AsNoTracking().Where(p => p.BoutiqueId == shop).Include(p => p.Payments)
            .OrderByDescending(p => p.RecordedAt).ThenByDescending(p => p.Id).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? supplierId)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        await PrepareAsync(shop, purchase: true);
        return View(new PurchaseInput { SupplierId = supplierId ?? 0, CurrencyCode = (await Tenant.GetContextAsync()).PrimaryCurrency.Code });
    }

    [HttpPost]
    public async Task<IActionResult> Create(PurchaseInput model)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                var purchase = await commerce.BuyAsync(shop, model);
                TempData["SuccessMessage"] = purchase.ReceivedAt.HasValue ? "Achat enregistré et stock mis à jour." : "Achat enregistré en attente de réception.";
                return RedirectToAction(nameof(Details), new { id = purchase.Id });
            }
            catch (CommerceRuleException error) { ModelState.AddModelError("", error.Message); }
            catch (CommerceNotFoundException) { return NotFound(); }
        }
        await PrepareAsync(shop, purchase: true);
        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        var purchase = await Db.Purchases.AsNoTracking().Include(p => p.Items).Include(p => p.Payments)
            .SingleOrDefaultAsync(p => p.BoutiqueId == shop && p.Id == id);
        return purchase == null ? NotFound() : View(purchase);
    }

    [HttpPost]
    public async Task<IActionResult> Receive(int id)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        try
        {
            TempData["SuccessMessage"] = await commerce.ReceiveAsync(shop, id) ? "Réception confirmée. Le stock a été mis à jour." : "Cet achat a déjà été réceptionné.";
        }
        catch (CommerceRuleException error) { TempData["ErrorMessage"] = error.Message; }
        catch (CommerceNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Details), new { id });
    }
}
