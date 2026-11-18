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
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantContextService(
            ApplicationDbContext db,
            IBoutiqueService boutiqueService,
            IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _boutiqueService = boutiqueService;
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
                settings = await _db.StoreSettings.AsNoTracking().OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(s => s.BoutiqueId == boutiqueId.Value);
                if (settings == null)
                    settings = CommerceWriter.DefaultSettings(await _boutiqueService.GetCurrentBoutiqueAsync());
            }

            settings ??= CommerceWriter.DefaultSettings(null);
            const string resolvedLang = "fr";
            var countryProfile = CountryProfileCatalog.GetCountry("CD");
            var primaryCurrency = CountryProfileCatalog.GetCurrency(settings.PrimaryCurrencyCode);
            var secondaryCurrency = Money.IsMvpCurrency(primaryCurrency.Code) && settings.CdfPerUsd is > 0
                ? CountryProfileCatalog.GetCurrency(primaryCurrency.Code == "CDF" ? "USD" : "CDF") : null;

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
