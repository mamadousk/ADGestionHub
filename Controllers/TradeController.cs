using AdGestionHub.Data;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

public abstract class TradeController(ApplicationDbContext db, IBoutiqueService boutiques, ITenantContextService tenant) : Controller
{
    protected ApplicationDbContext Db { get; } = db;
    protected ITenantContextService Tenant { get; } = tenant;

    protected Task<int?> ShopAsync() => boutiques.GetCurrentBoutiqueIdAsync();

    protected async Task PrepareAsync(int shop)
    {
        var settings = (await Tenant.GetContextAsync()).Settings;
        ViewData["TradeSettings"] = settings;
        var products = await Db.Products.AsNoTracking().Where(p => p.BoutiqueId == shop && !p.IsArchived
            && p.StockQuantity > 0).OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Sku, p.StockQuantity, p.SalePrice }).ToListAsync();
        ViewData["TradeProducts"] = products.Select(p => new CatalogPick(p.Id, p.Name, p.Sku, p.StockQuantity,
            !Money.IsMvpCurrency(settings.PrimaryCurrencyCode) ? new Dictionary<string, decimal>() :
            Money.AvailableCurrencies(settings).ToDictionary(code => code,
                code => Money.Convert(p.SalePrice, settings.PrimaryCurrencyCode, code, settings.CdfPerUsd)))).ToList();
        ViewData["TradeContacts"] = await Db.Clients.AsNoTracking().Where(c => c.BoutiqueId == shop).OrderBy(c => c.Name)
            .Select(c => new ContactPick(c.Id, c.Name)).ToListAsync();
    }
}
