using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBoutiqueService _boutiqueService;
        private readonly ITenantContextService _tenantContext;
        private readonly ICacheService _cacheService;
        private readonly IAppLocalizer _L;

        public SettingsController(
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

        // GET: Settings
        public async Task<IActionResult> Index(string? tab = null)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null)
            {
                if (User.IsInRole("SuperAdmin"))
                    return RedirectToAction("Index", "SuperAdmin");
                return Challenge();
            }

            var boutique = await _context.Boutiques.FindAsync(boutiqueId.Value);
            if (boutique == null) return NotFound();

            var settings = await _context.StoreSettings
                .FirstOrDefaultAsync(s => s.BoutiqueId == boutiqueId.Value);

            if (settings == null)
            {
                var defCountry = CountryProfileCatalog.GetCountry("CD");
                settings = new StoreSettings
                {
                    BoutiqueId = boutiqueId.Value,
                    StoreName = boutique.Nom,
                    Address = boutique.Adresse,
                    Phone = boutique.Telephone,
                    ContactEmail = boutique.Email,
                    CountryCode = defCountry.Code,
                    PreferredLanguage = "fr",
                    PrimaryCurrencyCode = defCountry.PrimaryCurrencyCode,
                    SecondaryCurrencyCode = defCountry.SuggestedSecondaryCurrencyCode,
                    SecondaryExchangeRate = 2850m,
                    PhoneDialCode = defCountry.PhoneDialCode,
                    DateFormat = defCountry.DateFormat,
                    DefaultVatRate = defCountry.StandardVatRate,
                    FooterMessage = "Merci de votre confiance.",
                    IsActive = boutique.IsActive
                };
                _context.StoreSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            await PopulateSettingsViewBagAsync(boutiqueId.Value, settings, tab);
            return View(settings);
        }

        // POST: Settings/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SettingsUpdateViewModel input)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            if (input.BoutiqueId != boutiqueId.Value)
                return Forbid();

            var boutique = await _context.Boutiques.FindAsync(boutiqueId.Value);
            if (boutique == null) return NotFound();

            var existing = await _context.StoreSettings
                .FirstOrDefaultAsync(s => s.BoutiqueId == boutiqueId.Value);

            if (existing == null) return NotFound();

            var country = CountryProfileCatalog.GetCountry(input.CountryCode);
            var primaryCurrency = CountryProfileCatalog.GetCurrency(input.PrimaryCurrencyCode);
            var normalizedLang = string.Equals(input.PreferredLanguage, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fr";

            // Exigence métier : en mode Formalisé, l'identifiant fiscal légal du pays est obligatoire
            if (input.IsFormalized && string.IsNullOrWhiteSpace(input.TaxIdentificationNumber))
            {
                var taxLabel = normalizedLang == "en" ? country.TaxIdLabelEn : country.TaxIdLabelFr;
                ModelState.AddModelError(
                    nameof(input.TaxIdentificationNumber),
                    normalizedLang == "en"
                        ? $"{taxLabel} is required when Formalized status is enabled."
                        : $"Le champ {taxLabel} est obligatoire lorsque le statut Formalisé est activé.");
            }

            if (input.SecondaryCurrencyEnabled)
            {
                if (string.IsNullOrWhiteSpace(input.SecondaryCurrencyCode)
                    || string.Equals(input.SecondaryCurrencyCode, primaryCurrency.Code, StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(
                        nameof(input.SecondaryCurrencyCode),
                        normalizedLang == "en"
                            ? "Please select a secondary currency distinct from the primary currency."
                            : "Veuillez sélectionner une devise secondaire distincte de la devise principale.");
                }
            }

            if (!ModelState.IsValid)
            {
                // Réinjecter les valeurs saisies dans l'objet affiché
                ApplyToEntity(existing, input, country, primaryCurrency, normalizedLang);
                await PopulateSettingsViewBagAsync(boutiqueId.Value, existing, input.ActiveTab ?? "fiscal");
                return View("Index", existing);
            }

            var prevSummary = $"Pays={existing.CountryCode}, Devise={existing.PrimaryCurrencyCode}, Formalisé={existing.IsFormalized}, TVA={existing.IsVatEnabled} ({existing.DefaultVatRate}%)";

            ApplyToEntity(existing, input, country, primaryCurrency, normalizedLang);

            // Synchroniser les coordonnées principales sur l'entité Boutique
            boutique.Nom = existing.StoreName;
            boutique.Adresse = existing.Address ?? string.Empty;
            boutique.Telephone = existing.Phone ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(existing.ContactEmail))
                boutique.Email = existing.ContactEmail;

            await _context.SaveChangesAsync();
            await _cacheService.RemoveAsync($"Dashboard_{boutiqueId.Value}");

            var newSummary = $"Pays={existing.CountryCode}, Devise={existing.PrimaryCurrencyCode}, Formalisé={existing.IsFormalized}, TVA={existing.IsVatEnabled} ({existing.DefaultVatRate}%)";
            if (!string.Equals(prevSummary, newSummary, StringComparison.Ordinal))
            {
                await _tenantContext.LogAuditAsync(
                    boutiqueId.Value,
                    "Mise à jour des paramètres fiscaux / localisation (applicable aux nouvelles opérations uniquement)",
                    $"Avant : {prevSummary} | Après : {newSummary}");
            }

            // Aligner immédiatement le cookie de langue de l'administrateur
            Response.Cookies.Append(
                AppLocalizer.LanguageCookieName,
                normalizedLang,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });

            TempData["SuccessMessage"] = normalizedLang == "en"
                ? "Settings saved. Changes apply exclusively to new transactions."
                : "Paramètres enregistrés. Les modifications s'appliquent exclusivement aux nouvelles opérations.";

            return RedirectToAction(nameof(Index), new { tab = input.ActiveTab });
        }

        // POST: Settings/NotifyEInvoicing
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotifyEInvoicing()
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            var settings = await _context.StoreSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.BoutiqueId == boutiqueId.Value);

            var countryCode = settings?.CountryCode ?? "CD";
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var email = User.Identity?.Name ?? settings?.ContactEmail ?? string.Empty;

            var alreadyExists = await _context.EInvoicingOptIns
                .AnyAsync(o => o.BoutiqueId == boutiqueId.Value && o.CountryCode == countryCode);

            if (!alreadyExists)
            {
                _context.EInvoicingOptIns.Add(new EInvoicingOptIn
                {
                    BoutiqueId = boutiqueId.Value,
                    UserId = userId,
                    Email = email,
                    CountryCode = countryCode,
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
                await _tenantContext.LogAuditAsync(boutiqueId.Value, $"Inscription notification Facturation Électronique Certifiée ({countryCode})");
            }

            TempData["SuccessMessage"] = _L.T(
                "Votre demande a été enregistrée. Vous serez informé dès l'ouverture de la Facturation Électronique Certifiée dans votre pays.",
                "Your request has been registered. You will be notified as soon as Certified Electronic Invoicing launches in your country.");

            return RedirectToAction(nameof(Index), new { tab = "fiscal" });
        }

        private static void ApplyToEntity(
            StoreSettings target,
            SettingsUpdateViewModel input,
            CountryProfile country,
            CurrencyInfo primaryCurrency,
            string normalizedLang)
        {
            target.StoreName = string.IsNullOrWhiteSpace(input.StoreName) ? target.StoreName : input.StoreName.Trim();
            target.Address = input.Address?.Trim();
            target.Phone = input.Phone?.Trim();
            target.ContactEmail = input.ContactEmail?.Trim();
            target.Description = input.Description?.Trim();
            target.FooterMessage = input.FooterMessage?.Trim();

            target.CountryCode = country.Code;
            target.PreferredLanguage = normalizedLang;
            target.PhoneDialCode = country.PhoneDialCode;
            target.DateFormat = country.DateFormat;

            target.PrimaryCurrencyCode = primaryCurrency.Code;
            target.SecondaryCurrencyEnabled = input.SecondaryCurrencyEnabled;
            target.SecondaryCurrencyCode = string.IsNullOrWhiteSpace(input.SecondaryCurrencyCode)
                ? country.SuggestedSecondaryCurrencyCode
                : CountryProfileCatalog.GetCurrency(input.SecondaryCurrencyCode).Code;
            target.SecondaryExchangeRate = input.SecondaryExchangeRate > 0m ? decimal.Round(input.SecondaryExchangeRate, 6) : 1m;

            target.IsFormalized = input.IsFormalized;
            target.TaxIdentificationNumber = input.TaxIdentificationNumber?.Trim();
            target.TradeRegisterNumber = input.TradeRegisterNumber?.Trim();
            target.IsVatEnabled = input.IsVatEnabled;
            target.DefaultVatRate = decimal.Round(Math.Clamp(input.DefaultVatRate, 0m, 100m), 2);
        }

        private async Task PopulateSettingsViewBagAsync(int boutiqueId, StoreSettings settings, string? activeTab)
        {
            var optedIn = await _context.EInvoicingOptIns
                .AsNoTracking()
                .AnyAsync(o => o.BoutiqueId == boutiqueId && o.CountryCode == settings.CountryCode);

            var auditLogs = await _context.SystemLogs
                .AsNoTracking()
                .Where(l => l.BoutiqueId == boutiqueId && l.ErrorMessage.StartsWith("[AUDIT]"))
                .OrderByDescending(l => l.DateOccurred)
                .Take(25)
                .ToListAsync();

            ViewBag.EInvoicingOptedIn = optedIn;
            ViewBag.AuditLogs = auditLogs;
            ViewBag.ActiveTab = string.IsNullOrWhiteSpace(activeTab) ? "general" : activeTab.ToLowerInvariant();
            ViewBag.Countries = CountryProfileCatalog.AllCountries;
            ViewBag.Currencies = CountryProfileCatalog.AllCurrencies;
        }
    }
}
