using AdGestionHub.Data;
using AdGestionHub.Models;
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
    public Task<IActionResult> Index() => ShowAsync(false);

    [HttpGet]
    public Task<IActionResult> Setup() => ShowAsync(true);

    [HttpPost, RequestSizeLimit(StoreLogoImages.MaxFormBytes)]
    public Task<IActionResult> Setup(ShopSettingsInput model) => SaveAsync(model, true);

    [HttpPost]
    public async Task<IActionResult> SkipSetup()
    {
        var boutique = await boutiques.GetCurrentBoutiqueAsync();
        if (boutique == null) return Forbid();
        if (!boutique.SetupPending) return RedirectToAction("Index", "Home");
        try
        {
            await writer.ExecuteAsync(boutique.Id, () => db.Boutiques.Where(b => b.Id == boutique.Id && b.SetupPending)
                .ExecuteUpdateAsync(u => u.SetProperty(b => b.SetupPending, false)));
        }
        catch (CommerceNotFoundException) { return NotFound(); }
        TempData["SuccessMessage"] = "Vous pourrez personnaliser votre boutique à tout moment dans les paramètres.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public async Task<IActionResult> Audit()
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        return View(await db.SystemLogs.AsNoTracking().Where(l => l.BoutiqueId == shop && l.ErrorMessage.StartsWith(SystemLog.AuditPrefix))
            .OrderByDescending(l => l.DateOccurred).ThenByDescending(l => l.Id).Take(100).ToListAsync());
    }

    [HttpPost, RequestSizeLimit(StoreLogoImages.MaxFormBytes)]
    public Task<IActionResult> Edit(ShopSettingsInput model) => SaveAsync(model, false);

    private async Task<IActionResult> ShowAsync(bool setup)
    {
        var boutique = await boutiques.GetCurrentBoutiqueAsync();
        if (boutique == null) return Forbid();
        if (setup && !boutique.SetupPending) return RedirectToAction(nameof(Index));
        var settings = (await tenant.GetContextAsync()).Settings;
        return await RenderAsync(boutique.Id, new ShopSettingsInput
        {
            StoreName = settings.StoreName, Address = settings.Address, Phone = settings.Phone, ContactEmail = settings.ContactEmail,
            Description = settings.Description, FooterMessage = settings.FooterMessage, PrimaryCurrencyCode = settings.PrimaryCurrencyCode,
            CdfPerUsd = settings.CdfPerUsd, CustomPaymentMethods = settings.CustomPaymentMethods
        }, setup);
    }

    private async Task<IActionResult> SaveAsync(ShopSettingsInput model, bool setup)
    {
        var boutique = await boutiques.GetCurrentBoutiqueAsync();
        if (boutique == null) return Forbid();
        if (setup && !boutique.SetupPending) return RedirectToAction(nameof(Index));
        if (model.Logo != null && model.RemoveLogo)
            ModelState.AddModelError(nameof(model.Logo), "Choisissez soit une nouvelle image, soit la suppression du logo actuel.");

        byte[]? png = null;
        if (ModelState.IsValid && model.Logo != null)
        {
            try { png = await StoreLogoImages.NormalizeAsync(model.Logo, HttpContext.RequestAborted); }
            catch (CommerceRuleException error) { ModelState.AddModelError(nameof(model.Logo), error.Message); }
        }

        if (ModelState.IsValid)
        {
            try
            {
                var saved = await writer.ExecuteAsync(boutique.Id, async () =>
                {
                    var shop = await db.Boutiques.SingleAsync(b => b.Id == boutique.Id);
                    if (setup && !shop.SetupPending) return false; // A second tab may already have finished setup.
                    var settings = await writer.SettingsAsync(boutique.Id);
                    if (model.PrimaryCurrencyCode != settings.PrimaryCurrencyCode)
                    {
                        if (!Money.IsMvpCurrency(model.PrimaryCurrencyCode)) throw new CommerceRuleException("Choisissez le CDF ou l'USD.");
                        if (await CurrencyLockedAsync(boutique.Id))
                            throw new CommerceRuleException("La devise du catalogue ne peut plus être modifiée après l'ajout d'un produit ou d'une opération.");
                    }
                    var methods = PaymentMeans.Custom(model.CustomPaymentMethods);
                    if (methods.Length > 8 || methods.Any(m => m.Length > 50 || m.Any(char.IsControl)))
                        throw new CommerceRuleException("Ajoutez jusqu'à 8 moyens de paiement, un par ligne (50 caractères chacun).");
                    var rate = Money.Rate(model.CdfPerUsd);
                    var oldRate = settings.CdfPerUsd;
                    settings.StoreName = shop.Nom = model.StoreName.Trim();
                    settings.Address = CommerceOperations.Clean(model.Address);
                    settings.Phone = CommerceOperations.Clean(model.Phone);
                    settings.ContactEmail = CommerceOperations.Clean(model.ContactEmail);
                    settings.Description = CommerceOperations.Clean(model.Description);
                    settings.FooterMessage = CommerceOperations.Clean(model.FooterMessage);
                    settings.PrimaryCurrencyCode = model.PrimaryCurrencyCode;
                    settings.CdfPerUsd = rate;
                    settings.CountryCode = "CD";
                    settings.PreferredLanguage = "fr";
                    settings.CustomPaymentMethods = methods.Length == 0 ? null : string.Join('\n', methods);
                    shop.Adresse = settings.Address ?? "";
                    shop.Telephone = settings.Phone ?? "";
                    if (setup) shop.SetupPending = false;

                    var details = new List<string>();
                    if (oldRate != rate) details.Add($"Taux CDF par USD : {oldRate?.ToString() ?? "non configuré"} → {rate?.ToString() ?? "non configuré"}");
                    if (png != null || model.RemoveLogo)
                    {
                        var logo = await db.StoreLogos.SingleOrDefaultAsync(l => l.BoutiqueId == shop.Id);
                        if (png != null)
                        {
                            if (logo == null) db.StoreLogos.Add(new StoreLogo { BoutiqueId = shop.Id, Png = png });
                            else logo.Png = png;
                            details.Add("Logo mis à jour");
                        }
                        else if (logo != null)
                        {
                            db.StoreLogos.Remove(logo);
                            details.Add("Logo supprimé");
                        }
                    }
                    await writer.AuditAsync(shop.Id, "Paramètres de l'entreprise modifiés", details.Count == 0 ? null : string.Join(" · ", details));
                    return true;
                });
                if (!saved) return RedirectToAction("Index", "Home");
                TempData["SuccessMessage"] = setup ? "Votre boutique est prête. Ajoutez un premier produit pour commencer."
                    : "Paramètres enregistrés. Les anciens montants et taux ne changent pas.";
                return setup ? RedirectToAction("Index", "Home") : RedirectToAction(nameof(Index));
            }
            catch (CommerceRuleException error) { ModelState.AddModelError("", error.Message); }
            catch (CommerceNotFoundException) { return NotFound(); }
        }
        return await RenderAsync(boutique.Id, model, setup);
    }

    private async Task<IActionResult> RenderAsync(int shop, ShopSettingsInput model, bool setup)
    {
        ViewData["CurrencyLocked"] = await CurrencyLockedAsync(shop);
        ViewData["Setup"] = setup;
        var logo = await db.StoreLogos.AsNoTracking().Where(l => l.BoutiqueId == shop).Select(l => l.Png).SingleOrDefaultAsync();
        if (logo != null) ViewData["LogoDataUrl"] = StoreLogoImages.DataUrl(logo);
        return View("Index", model);
    }

    private async Task<bool> CurrencyLockedAsync(int shop) => await db.Products.AnyAsync(p => p.BoutiqueId == shop)
        || await db.Sales.AnyAsync(s => s.BoutiqueId == shop) || await db.Quotes.AnyAsync(q => q.BoutiqueId == shop)
        || await db.Purchases.AnyAsync(p => p.BoutiqueId == shop) || await db.Expenses.AnyAsync(e => e.BoutiqueId == shop)
        || await db.Payments.AnyAsync(p => p.BoutiqueId == shop) || await db.Debts.AnyAsync(d => d.BoutiqueId == shop);
}
