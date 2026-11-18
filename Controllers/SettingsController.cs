using AdGestionHub.Data;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

[Authorize(Roles = "Admin")]
public sealed class SettingsController(ApplicationDbContext db, IBoutiqueService boutiques, ITenantContextService tenant,
    CommerceWriter writer) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var settings = (await tenant.GetContextAsync()).Settings;
        ViewData["CurrencyLocked"] = await CurrencyLockedAsync(shop);
        return View(new ShopSettingsInput
        {
            StoreName = settings.StoreName, Address = settings.Address, Phone = settings.Phone, ContactEmail = settings.ContactEmail,
            FooterMessage = settings.FooterMessage, PrimaryCurrencyCode = settings.PrimaryCurrencyCode,
            CdfPerUsd = settings.CdfPerUsd, CustomPaymentMethods = settings.CustomPaymentMethods
        });
    }

    [HttpGet]
    public async Task<IActionResult> Audit()
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        return View(await db.SystemLogs.AsNoTracking().Where(l => l.BoutiqueId == shop && l.ErrorMessage.StartsWith("[AUDIT] "))
            .OrderByDescending(l => l.DateOccurred).ThenByDescending(l => l.Id).Take(100).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Edit(ShopSettingsInput model)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                await writer.ExecuteAsync(shop, async () =>
                {
                    var settings = await writer.SettingsAsync(shop);
                    if (model.PrimaryCurrencyCode != settings.PrimaryCurrencyCode)
                    {
                        if (!Money.IsMvpCurrency(model.PrimaryCurrencyCode)) throw new CommerceRuleException("Choisissez le CDF ou l'USD.");
                        if (await CurrencyLockedAsync(shop))
                            throw new CommerceRuleException("La devise du catalogue ne peut plus être modifiée après l'ajout d'un produit ou d'une opération.");
                    }
                    var methods = PaymentMeans.Custom(model.CustomPaymentMethods);
                    if (methods.Length > 8 || methods.Any(m => m.Length > 50 || m.Any(char.IsControl)))
                        throw new CommerceRuleException("Ajoutez jusqu'à 8 moyens de paiement, un par ligne (50 caractères chacun).");
                    var rate = Money.Rate(model.CdfPerUsd);
                    var oldRate = settings.CdfPerUsd;
                    var boutique = await db.Boutiques.SingleAsync(b => b.Id == shop);
                    settings.StoreName = boutique.Nom = model.StoreName.Trim();
                    settings.Address = CommerceOperations.Clean(model.Address);
                    settings.Phone = CommerceOperations.Clean(model.Phone);
                    settings.ContactEmail = CommerceOperations.Clean(model.ContactEmail);
                    settings.FooterMessage = CommerceOperations.Clean(model.FooterMessage);
                    settings.PrimaryCurrencyCode = model.PrimaryCurrencyCode;
                    settings.CdfPerUsd = rate;
                    settings.CountryCode = "CD";
                    settings.PreferredLanguage = "fr";
                    settings.CustomPaymentMethods = methods.Length == 0 ? null : string.Join('\n', methods);
                    boutique.Adresse = settings.Address ?? "";
                    boutique.Telephone = settings.Phone ?? "";
                    writer.Audit(shop, "Paramètres de l'entreprise modifiés",
                        oldRate == rate ? null : $"Taux CDF par USD : {oldRate?.ToString() ?? "non configuré"} → {rate?.ToString() ?? "non configuré"}");
                    return true;
                });
                TempData["SuccessMessage"] = "Paramètres enregistrés. Les anciens montants et taux ne changent pas.";
                return RedirectToAction(nameof(Index));
            }
            catch (CommerceRuleException error) { ModelState.AddModelError("", error.Message); }
            catch (CommerceNotFoundException) { return NotFound(); }
        }
        ViewData["CurrencyLocked"] = await CurrencyLockedAsync(shop);
        return View("Index", model);
    }

    private async Task<bool> CurrencyLockedAsync(int shop) => await db.Products.AnyAsync(p => p.BoutiqueId == shop)
        || await db.Sales.AnyAsync(s => s.BoutiqueId == shop) || await db.Quotes.AnyAsync(q => q.BoutiqueId == shop)
        || await db.Purchases.AnyAsync(p => p.BoutiqueId == shop) || await db.Expenses.AnyAsync(e => e.BoutiqueId == shop)
        || await db.Payments.AnyAsync(p => p.BoutiqueId == shop) || await db.Debts.AnyAsync(d => d.BoutiqueId == shop);
}
