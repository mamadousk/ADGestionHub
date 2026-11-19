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

    protected async Task PrepareAsync(int shop, bool includeContacts)
    {
        var settings = (await Tenant.GetContextAsync()).Settings;
        ViewData["TradeSettings"] = settings;
        var products = await Db.Products.AsNoTracking().Where(p => p.BoutiqueId == shop && !p.IsArchived
            && p.StockQuantity > 0).OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.Name, p.Sku, p.Category, p.StockQuantity, p.StockAlertThreshold, p.SalePrice }).ToListAsync();
        var codes = Money.IsMvpCurrency(settings.PrimaryCurrencyCode) ? Money.AvailableCurrencies(settings) : Array.Empty<string>();
        ViewData["TradeProducts"] = products.Select(p =>
        {
            var prices = new Dictionary<string, decimal>();
            foreach (var code in codes)
            {
                var price = Money.Convert(p.SalePrice, settings.PrimaryCurrencyCode, code, settings.CdfPerUsd);
                if (price >= 0 && price <= Money.MaximumAmount && (price > 0 || p.SalePrice == 0))
                    prices.Add(code, price);
            }
            return new CatalogPick(p.Id, p.Name, p.Sku, p.Category, p.StockQuantity, p.StockAlertThreshold, prices);
        }).ToList();
        ViewData["TradeContacts"] = includeContacts
            ? await Db.Clients.AsNoTracking().Where(c => c.BoutiqueId == shop).OrderBy(c => c.Name)
                .Select(c => new ContactPick(c.Id, c.Name)).ToListAsync()
            : Array.Empty<ContactPick>();
    }
}
