using System;
using System.Linq;
using System.Threading.Tasks;
using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Services
{
    public sealed class TenantContextService : ITenantContextService
    {
        private const string HttpCacheKey = "AdGestionHub.TenantFiscalContext";

        private readonly ApplicationDbContext _db;
        private readonly IBoutiqueService _boutiqueService;
        private readonly IAppLocalizer _localizer;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantContextService(
            ApplicationDbContext db,
            IBoutiqueService boutiqueService,
            IAppLocalizer localizer,
            IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _boutiqueService = boutiqueService;
            _localizer = localizer;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<TenantFiscalContext> GetContextAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null && httpContext.Items.TryGetValue(HttpCacheKey, out var cached) && cached is TenantFiscalContext cachedCtx)
            {
                return cachedCtx;
            }

            StoreSettings? settings = null;
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();

            if (boutiqueId.HasValue)
            {
                settings = await _db.StoreSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.BoutiqueId == boutiqueId.Value);

                if (settings == null)
                {
                    var boutique = await _db.Boutiques.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boutiqueId.Value);
                    if (boutique != null)
                    {
                        var country = CountryProfileCatalog.GetCountry("CD");
                        settings = new StoreSettings
                        {
                            BoutiqueId = boutique.Id,
                            StoreName = boutique.Nom,
                            Address = boutique.Adresse,
                            Phone = boutique.Telephone,
                            ContactEmail = boutique.Email,
                            CountryCode = country.Code,
                            PreferredLanguage = "fr",
                            PrimaryCurrencyCode = country.PrimaryCurrencyCode,
                            SecondaryCurrencyCode = country.SuggestedSecondaryCurrencyCode,
                            SecondaryExchangeRate = 2850m,
                            PhoneDialCode = country.PhoneDialCode,
                            DateFormat = country.DateFormat,
                            DefaultVatRate = country.StandardVatRate,
                            FooterMessage = "Merci de votre confiance."
                        };
                        _db.StoreSettings.Add(settings);
                        await _db.SaveChangesAsync();
                    }
                }
            }

            settings ??= new StoreSettings
            {
                StoreName = "AdGestionHub",
                CountryCode = "CD",
                PreferredLanguage = "fr",
                PrimaryCurrencyCode = "CDF",
                SecondaryCurrencyCode = "USD",
                SecondaryExchangeRate = 2850m,
                PhoneDialCode = "+243",
                DateFormat = "dd/MM/yyyy",
                DefaultVatRate = 16m
            };

            // Résoudre la langue : cookie explicite > préférence boutique > "fr"
            string resolvedLang = _localizer.CurrentLanguage;
            if (httpContext != null && !httpContext.Request.Cookies.ContainsKey(AppLocalizer.LanguageCookieName))
            {
                if (!string.IsNullOrWhiteSpace(settings.PreferredLanguage))
                {
                    resolvedLang = string.Equals(settings.PreferredLanguage, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fr";
                }
            }

            if (httpContext != null)
            {
                httpContext.Items["ResolvedLanguage"] = resolvedLang;
            }

            var countryProfile = CountryProfileCatalog.GetCountry(settings.CountryCode);
            var primaryCurrency = CountryProfileCatalog.GetCurrency(settings.PrimaryCurrencyCode);
            var secondaryCurrency = settings.SecondaryCurrencyEnabled && !string.IsNullOrWhiteSpace(settings.SecondaryCurrencyCode)
                ? CountryProfileCatalog.GetCurrency(settings.SecondaryCurrencyCode)
                : null;

            var ctx = new TenantFiscalContext
            {
                Settings = settings,
                Country = countryProfile,
                PrimaryCurrency = primaryCurrency,
                SecondaryCurrency = secondaryCurrency,
                Language = resolvedLang
            };

            if (httpContext != null)
            {
                httpContext.Items[HttpCacheKey] = ctx;
            }

            return ctx;
        }

        public async Task<string> AllocateNextDocumentNumberAsync(int boutiqueId, string documentKind)
        {
            var settings = await _db.StoreSettings.FirstOrDefaultAsync(s => s.BoutiqueId == boutiqueId);
            if (settings == null)
            {
                var fallbackCount = await _db.Sales.CountAsync(s => s.BoutiqueId == boutiqueId) + 1;
                return documentKind == "Invoice" ? $"FAC-{fallbackCount:D5}"
                     : documentKind == "Quote" ? $"PRO-{fallbackCount:D5}"
                     : $"REC-{fallbackCount:D5}";
            }

            if (string.Equals(documentKind, "Invoice", StringComparison.OrdinalIgnoreCase))
            {
                if (settings.NextInvoiceNumber <= 1)
                {
                    var existingInvoices = await _db.Sales.CountAsync(s => s.BoutiqueId == boutiqueId && s.DocumentType == "Invoice");
                    if (existingInvoices >= settings.NextInvoiceNumber)
                        settings.NextInvoiceNumber = existingInvoices + 1;
                }
                var num = settings.NextInvoiceNumber;
                settings.NextInvoiceNumber = num + 1;
                await _db.SaveChangesAsync();
                return $"FAC-{num:D5}";
            }

            if (string.Equals(documentKind, "Quote", StringComparison.OrdinalIgnoreCase))
            {
                if (settings.NextQuoteNumber <= 1)
                {
                    var existingQuotes = await _db.Quotes.CountAsync(q => q.BoutiqueId == boutiqueId);
                    if (existingQuotes >= settings.NextQuoteNumber)
                        settings.NextQuoteNumber = existingQuotes + 1;
                }
                var num = settings.NextQuoteNumber;
                settings.NextQuoteNumber = num + 1;
                await _db.SaveChangesAsync();
                return $"PRO-{num:D5}";
            }

            if (settings.NextReceiptNumber <= 1)
            {
                var existingReceipts = await _db.Sales.CountAsync(s => s.BoutiqueId == boutiqueId && s.DocumentType != "Invoice");
                if (existingReceipts >= settings.NextReceiptNumber)
                    settings.NextReceiptNumber = existingReceipts + 1;
            }
            var recNum = settings.NextReceiptNumber;
            settings.NextReceiptNumber = recNum + 1;
            await _db.SaveChangesAsync();
            return $"REC-{recNum:D5}";
        }

        public async Task LogAuditAsync(int? boutiqueId, string actionSummary, string? details = null)
        {
            var userEmail = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
            _db.SystemLogs.Add(new SystemLog
            {
                BoutiqueId = boutiqueId,
                DateOccurred = DateTime.Now,
                UserEmail = userEmail,
                ErrorMessage = $"[AUDIT] {actionSummary}",
                StackTrace = details
            });
            await _db.SaveChangesAsync();
        }
    }
}
