using System.Security.Claims;
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
    CommerceOperations commerce, IPdfReportService pdf, StaffAccess access) : TradeController(db, boutiques, tenant)
{
    [Authorize(Policy = StaffAccessRules.SalesHistory)]
    public async Task<IActionResult> Index()
    {
        if (await ShopAsync() is not int shop) return Forbid();
        var showProfit = (await access.CurrentAsync()).Has(StaffPermission.Profit);
        IQueryable<Sale> query = Db.Sales.AsNoTracking().Where(s => s.BoutiqueId == shop).Include(s => s.Payments);
        if (showProfit) query = query.Include(s => s.Items.Where(i => i.BoutiqueId == shop)).AsSplitQuery();
        var sales = await query.OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.Id).ToListAsync(HttpContext.RequestAborted);
        if (showProfit) ViewData["SaleProfits"] = sales.ToDictionary(s => s.Id, SaleProfit.ForSale);
        return View(sales);
    }

    [HttpGet, Authorize(Policy = StaffAccessRules.Sell)]
    public async Task<IActionResult> Create(int? productId)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        await PrepareAsync(shop, (await access.CurrentAsync()).Has(StaffPermission.Clients));
        return View(new SaleInput
        {
            CurrencyCode = (await Tenant.GetContextAsync()).PrimaryCurrency.Code,
            Items = [new TradeLineInput { ProductId = productId ?? 0 }]
        });
    }

    [HttpPost, Authorize(Policy = StaffAccessRules.Sell)]
    public async Task<IActionResult> Create(SaleInput model)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        var canUseDirectory = (await access.CurrentAsync()).Has(StaffPermission.Clients);
        if (model.ClientId.HasValue && !canUseDirectory) return Forbid();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                var sale = await commerce.SellAsync(shop, model, User.IsInRole("Admin"), userId);
                TempData["SuccessMessage"] = $"Vente {sale.Number} enregistrée. Le stock a été mis à jour.";
                if (sale.StockWarnings > 0)
                    TempData["StockNotice"] = $"{sale.StockWarnings} produit(s) de cette vente sont au seuil d’alerte ou en dessous.";
                return RedirectToAction(nameof(Details), new { id = sale.Id });
            }
            catch (CommerceRuleException error) { ModelState.AddModelError("", error.Message); }
            catch (CommerceNotFoundException) { return NotFound(); }
        }
        await PrepareAsync(shop, canUseDirectory);
        return View(model);
    }

    [Authorize(Policy = StaffAccessRules.SaleDocuments)]
    public async Task<IActionResult> Details(int id)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        var sale = await FindAsync(shop, id);
        if (sale == null) return NotFound();
        if ((await access.CurrentAsync()).Has(StaffPermission.SalesHistory | StaffPermission.Profit))
            ViewData["SaleProfit"] = SaleProfit.ForSale(sale);
        return View(sale);
    }

    [Authorize(Policy = StaffAccessRules.SaleDocuments)]
    public async Task<IActionResult> Receipt(int id, string? format = null)
    {
        if (await ShopAsync() is not int shop) return Forbid();
        var sale = await FindAsync(shop, id);
        if (sale == null) return NotFound();
        var settings = (await Tenant.GetContextAsync()).Settings;
        var logo = await Db.StoreLogos.AsNoTracking().Where(l => l.BoutiqueId == shop).Select(l => l.Png).SingleOrDefaultAsync();
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            return File(pdf.SalePdf(sale, settings, logo), "application/pdf", $"vente-{sale.Id}.pdf");
        ViewData["StoreSettings"] = settings;
        if (logo != null) ViewData["LogoDataUrl"] = StoreLogoImages.DataUrl(logo);
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

    private async Task<Sale?> FindAsync(int shop, int id)
    {
        var sales = Db.Sales.AsNoTracking().Where(s => s.BoutiqueId == shop && s.Id == id);
        if (!(await access.CurrentAsync()).Has(StaffPermission.SalesHistory))
        {
            // Recording a sale gives access to that seller's own document, not to other receipts.
            var owner = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(owner)) return null;
            sales = sales.Where(s => s.CreatedByUserId == owner);
        }
        return await sales.Include(s => s.Items.Where(i => i.BoutiqueId == shop))
            .Include(s => s.Payments).SingleOrDefaultAsync(HttpContext.RequestAborted);
    }
}
