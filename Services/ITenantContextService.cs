using System;
using System.Threading.Tasks;
using AdGestionHub.Models;

namespace AdGestionHub.Services
{
    public sealed class TenantFiscalContext
    {
        public StoreSettings Settings { get; init; } = new();
        public CountryProfile Country { get; init; } = CountryProfileCatalog.GetCountry("CD");
        public CurrencyInfo PrimaryCurrency { get; init; } = CountryProfileCatalog.GetCurrency("USD");
        public CurrencyInfo? SecondaryCurrency { get; init; }
        public string Language { get; init; } = "fr";

        public bool IsEnglish => string.Equals(Language, "en", StringComparison.OrdinalIgnoreCase);
        public bool IsFormalized => Settings.IsFormalized;
        public bool IsVatEnabled => Settings.IsVatEnabled;
        public decimal DefaultVatRate => Settings.DefaultVatRate;
        public string TaxIdLabel => IsEnglish ? Country.TaxIdLabelEn : Country.TaxIdLabelFr;
        public string TradeRegisterLabel => IsEnglish ? Country.TradeRegisterLabelEn : Country.TradeRegisterLabelFr;

        public string NumberInputStep => PrimaryCurrency.Decimals == 0 ? "1" : "0.01";

        public string FormatPrimary(decimal amount) =>
            CountryProfileCatalog.FormatAmount(amount, PrimaryCurrency.Code, PrimaryCurrency.Decimals, PrimaryCurrency.Symbol, Language);

        public string FormatHistorical(decimal amount, string? currencyCode, int? decimals = null, string? symbol = null) =>
            CountryProfileCatalog.FormatAmount(amount, currencyCode ?? PrimaryCurrency.Code, decimals, symbol, Language);

        public string? FormatSecondaryEquivalent(decimal primaryAmount)
        {
            if (SecondaryCurrency == null) return null;
            return Money.Display(Money.Convert(primaryAmount, PrimaryCurrency.Code, SecondaryCurrency.Code, Settings.CdfPerUsd), SecondaryCurrency.Code);
        }

        public string FormatDate(DateTime date, bool includeTime = false)
        {
            var dateFmt = string.IsNullOrWhiteSpace(Settings.DateFormat) ? Country.DateFormat : Settings.DateFormat;
            return includeTime ? date.ToString($"{dateFmt} HH:mm") : date.ToString(dateFmt);
        }

        public string FormatDateTime(DateTime date) => FormatDate(date, includeTime: true);
    }

    public interface ITenantContextService
    {
        Task<TenantFiscalContext> GetContextAsync();
        Task LogAuditAsync(int? boutiqueId, string actionSummary, string? details = null);
    }
}
