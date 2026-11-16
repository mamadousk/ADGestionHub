using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AdGestionHub.Services
{
    public sealed record CurrencyInfo(
        string Code,
        string Symbol,
        string NameFr,
        string NameEn,
        int Decimals);

    public sealed record CountryProfile(
        string Code,
        string NameFr,
        string NameEn,
        string DefaultLanguage,
        string PrimaryCurrencyCode,
        string SuggestedSecondaryCurrencyCode,
        string PhoneDialCode,
        string DateFormat,
        string TaxIdLabelFr,
        string TaxIdLabelEn,
        string TradeRegisterLabelFr,
        string TradeRegisterLabelEn,
        decimal StandardVatRate,
        decimal[] SuggestedVatRates,
        string[] DefaultPaymentMethods);

    public static class CountryProfileCatalog
    {
        private static readonly string[] FrancophoneMobileMethods =
            { "Cash", "Orange Money", "M-Pesa", "Airtel Money", "Wave", "Mobile Money", "Carte Bancaire", "Virement Bancaire" };

        private static readonly string[] AnglophoneMobileMethods =
            { "Cash", "M-Pesa", "Mobile Money", "Bank Transfer", "Card" };

        private static readonly Dictionary<string, CurrencyInfo> Currencies = new(StringComparer.OrdinalIgnoreCase)
        {
            ["CDF"] = new("CDF", "FC", "Franc congolais (CDF)", "Congolese Franc (CDF)", 2),
            ["XOF"] = new("XOF", "FCFA", "Franc CFA BCEAO (XOF)", "West African CFA Franc (XOF)", 0),
            ["XAF"] = new("XAF", "FCFA", "Franc CFA BEAC (XAF)", "Central African CFA Franc (XAF)", 0),
            ["USD"] = new("USD", "$", "Dollar américain (USD)", "US Dollar (USD)", 2),
            ["EUR"] = new("EUR", "€", "Euro (EUR)", "Euro (EUR)", 2),
            ["KES"] = new("KES", "KSh", "Shilling kényan (KES)", "Kenyan Shilling (KES)", 2),
            ["NGN"] = new("NGN", "₦", "Naira nigérian (NGN)", "Nigerian Naira (NGN)", 2),
            ["RWF"] = new("RWF", "FRw", "Franc rwandais (RWF)", "Rwandan Franc (RWF)", 0),
            ["ZAR"] = new("ZAR", "R", "Rand sud-africain (ZAR)", "South African Rand (ZAR)", 2),
            ["GNF"] = new("GNF", "FG", "Franc guinéen (GNF)", "Guinean Franc (GNF)", 0),
            ["GBP"] = new("GBP", "£", "Livre sterling (GBP)", "British Pound (GBP)", 2)
        };

        private static readonly List<CountryProfile> Countries = new()
        {
            new CountryProfile(
                Code: "CD",
                NameFr: "RD Congo",
                NameEn: "DR Congo",
                DefaultLanguage: "fr",
                PrimaryCurrencyCode: "CDF",
                SuggestedSecondaryCurrencyCode: "USD",
                PhoneDialCode: "+243",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "NIF",
                TaxIdLabelEn: "Tax ID (NIF)",
                TradeRegisterLabelFr: "RCCM",
                TradeRegisterLabelEn: "Trade Reg. (RCCM)",
                StandardVatRate: 16m,
                SuggestedVatRates: new[] { 0m, 8m, 16m },
                DefaultPaymentMethods: new[] { "Cash", "M-Pesa", "Airtel Money", "Orange Money", "Carte Bancaire", "Virement Bancaire" }),

            new CountryProfile(
                Code: "CI",
                NameFr: "Côte d'Ivoire",
                NameEn: "Ivory Coast",
                DefaultLanguage: "fr",
                PrimaryCurrencyCode: "XOF",
                SuggestedSecondaryCurrencyCode: "EUR",
                PhoneDialCode: "+225",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "NCC",
                TaxIdLabelEn: "Tax ID (NCC)",
                TradeRegisterLabelFr: "RCCM",
                TradeRegisterLabelEn: "Trade Reg. (RCCM)",
                StandardVatRate: 18m,
                SuggestedVatRates: new[] { 0m, 9m, 18m },
                DefaultPaymentMethods: new[] { "Cash", "Orange Money", "Wave", "MTN MoMo", "Carte Bancaire", "Virement Bancaire" }),

            new CountryProfile(
                Code: "SN",
                NameFr: "Sénégal",
                NameEn: "Senegal",
                DefaultLanguage: "fr",
                PrimaryCurrencyCode: "XOF",
                SuggestedSecondaryCurrencyCode: "EUR",
                PhoneDialCode: "+221",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "NINEA",
                TaxIdLabelEn: "Tax ID (NINEA)",
                TradeRegisterLabelFr: "RCCM",
                TradeRegisterLabelEn: "Trade Reg. (RCCM)",
                StandardVatRate: 18m,
                SuggestedVatRates: new[] { 0m, 10m, 18m },
                DefaultPaymentMethods: new[] { "Cash", "Wave", "Orange Money", "Free Money", "Carte Bancaire", "Virement Bancaire" }),

            new CountryProfile(
                Code: "CM",
                NameFr: "Cameroun",
                NameEn: "Cameroon",
                DefaultLanguage: "fr",
                PrimaryCurrencyCode: "XAF",
                SuggestedSecondaryCurrencyCode: "EUR",
                PhoneDialCode: "+237",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "NIU",
                TaxIdLabelEn: "Tax ID (NIU)",
                TradeRegisterLabelFr: "RCCM",
                TradeRegisterLabelEn: "Trade Reg. (RCCM)",
                StandardVatRate: 19.25m,
                SuggestedVatRates: new[] { 0m, 19.25m },
                DefaultPaymentMethods: new[] { "Cash", "Orange Money", "MTN MoMo", "Carte Bancaire", "Virement Bancaire" }),

            new CountryProfile(
                Code: "CG",
                NameFr: "Congo-Brazzaville",
                NameEn: "Republic of the Congo",
                DefaultLanguage: "fr",
                PrimaryCurrencyCode: "XAF",
                SuggestedSecondaryCurrencyCode: "USD",
                PhoneDialCode: "+242",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "NIU",
                TaxIdLabelEn: "Tax ID (NIU)",
                TradeRegisterLabelFr: "RCCM",
                TradeRegisterLabelEn: "Trade Reg. (RCCM)",
                StandardVatRate: 18m,
                SuggestedVatRates: new[] { 0m, 5m, 18m },
                DefaultPaymentMethods: new[] { "Cash", "Airtel Money", "MTN MoMo", "Carte Bancaire", "Virement Bancaire" }),

            new CountryProfile(
                Code: "ML",
                NameFr: "Mali",
                NameEn: "Mali",
                DefaultLanguage: "fr",
                PrimaryCurrencyCode: "XOF",
                SuggestedSecondaryCurrencyCode: "EUR",
                PhoneDialCode: "+223",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "NIF",
                TaxIdLabelEn: "Tax ID (NIF)",
                TradeRegisterLabelFr: "RCCM",
                TradeRegisterLabelEn: "Trade Reg. (RCCM)",
                StandardVatRate: 18m,
                SuggestedVatRates: new[] { 0m, 5m, 18m },
                DefaultPaymentMethods: FrancophoneMobileMethods),

            new CountryProfile(
                Code: "BF",
                NameFr: "Burkina Faso",
                NameEn: "Burkina Faso",
                DefaultLanguage: "fr",
                PrimaryCurrencyCode: "XOF",
                SuggestedSecondaryCurrencyCode: "EUR",
                PhoneDialCode: "+226",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "IFU",
                TaxIdLabelEn: "Tax ID (IFU)",
                TradeRegisterLabelFr: "RCCM",
                TradeRegisterLabelEn: "Trade Reg. (RCCM)",
                StandardVatRate: 18m,
                SuggestedVatRates: new[] { 0m, 10m, 18m },
                DefaultPaymentMethods: FrancophoneMobileMethods),

            new CountryProfile(
                Code: "BJ",
                NameFr: "Bénin",
                NameEn: "Benin",
                DefaultLanguage: "fr",
                PrimaryCurrencyCode: "XOF",
                SuggestedSecondaryCurrencyCode: "EUR",
                PhoneDialCode: "+229",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "IFU",
                TaxIdLabelEn: "Tax ID (IFU)",
                TradeRegisterLabelFr: "RCCM",
                TradeRegisterLabelEn: "Trade Reg. (RCCM)",
                StandardVatRate: 18m,
                SuggestedVatRates: new[] { 0m, 18m },
                DefaultPaymentMethods: FrancophoneMobileMethods),

            new CountryProfile(
                Code: "TG",
                NameFr: "Togo",
                NameEn: "Togo",
                DefaultLanguage: "fr",
                PrimaryCurrencyCode: "XOF",
                SuggestedSecondaryCurrencyCode: "EUR",
                PhoneDialCode: "+228",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "NIF",
                TaxIdLabelEn: "Tax ID (NIF)",
                TradeRegisterLabelFr: "RCCM",
                TradeRegisterLabelEn: "Trade Reg. (RCCM)",
                StandardVatRate: 18m,
                SuggestedVatRates: new[] { 0m, 10m, 18m },
                DefaultPaymentMethods: FrancophoneMobileMethods),

            new CountryProfile(
                Code: "GN",
                NameFr: "Guinée",
                NameEn: "Guinea",
                DefaultLanguage: "fr",
                PrimaryCurrencyCode: "GNF",
                SuggestedSecondaryCurrencyCode: "USD",
                PhoneDialCode: "+224",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "NIF",
                TaxIdLabelEn: "Tax ID (NIF)",
                TradeRegisterLabelFr: "RCCM",
                TradeRegisterLabelEn: "Trade Reg. (RCCM)",
                StandardVatRate: 18m,
                SuggestedVatRates: new[] { 0m, 18m },
                DefaultPaymentMethods: FrancophoneMobileMethods),

            new CountryProfile(
                Code: "KE",
                NameFr: "Kenya",
                NameEn: "Kenya",
                DefaultLanguage: "en",
                PrimaryCurrencyCode: "KES",
                SuggestedSecondaryCurrencyCode: "USD",
                PhoneDialCode: "+254",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "TIN (KRA PIN)",
                TaxIdLabelEn: "TIN (KRA PIN)",
                TradeRegisterLabelFr: "BRN",
                TradeRegisterLabelEn: "Business Reg. (BRN)",
                StandardVatRate: 16m,
                SuggestedVatRates: new[] { 0m, 8m, 16m },
                DefaultPaymentMethods: AnglophoneMobileMethods),

            new CountryProfile(
                Code: "NG",
                NameFr: "Nigeria",
                NameEn: "Nigeria",
                DefaultLanguage: "en",
                PrimaryCurrencyCode: "NGN",
                SuggestedSecondaryCurrencyCode: "USD",
                PhoneDialCode: "+234",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "TIN",
                TaxIdLabelEn: "TIN",
                TradeRegisterLabelFr: "RC Number",
                TradeRegisterLabelEn: "RC Number",
                StandardVatRate: 7.5m,
                SuggestedVatRates: new[] { 0m, 7.5m },
                DefaultPaymentMethods: new[] { "Cash", "Bank Transfer", "Card", "Mobile Money" }),

            new CountryProfile(
                Code: "RW",
                NameFr: "Rwanda",
                NameEn: "Rwanda",
                DefaultLanguage: "en",
                PrimaryCurrencyCode: "RWF",
                SuggestedSecondaryCurrencyCode: "USD",
                PhoneDialCode: "+250",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "TIN",
                TaxIdLabelEn: "TIN",
                TradeRegisterLabelFr: "RDB Code",
                TradeRegisterLabelEn: "RDB Code",
                StandardVatRate: 18m,
                SuggestedVatRates: new[] { 0m, 18m },
                DefaultPaymentMethods: new[] { "Cash", "MTN MoMo", "Airtel Money", "Card", "Bank Transfer" }),

            new CountryProfile(
                Code: "ZA",
                NameFr: "Afrique du Sud",
                NameEn: "South Africa",
                DefaultLanguage: "en",
                PrimaryCurrencyCode: "ZAR",
                SuggestedSecondaryCurrencyCode: "USD",
                PhoneDialCode: "+27",
                DateFormat: "yyyy/MM/dd",
                TaxIdLabelFr: "TIN / VAT No",
                TaxIdLabelEn: "TIN / VAT No",
                TradeRegisterLabelFr: "CIPC Reg.",
                TradeRegisterLabelEn: "CIPC Reg.",
                StandardVatRate: 15m,
                SuggestedVatRates: new[] { 0m, 15m },
                DefaultPaymentMethods: new[] { "Cash", "Card", "Bank Transfer" }),

            new CountryProfile(
                Code: "FR",
                NameFr: "France",
                NameEn: "France",
                DefaultLanguage: "fr",
                PrimaryCurrencyCode: "EUR",
                SuggestedSecondaryCurrencyCode: "USD",
                PhoneDialCode: "+33",
                DateFormat: "dd/MM/yyyy",
                TaxIdLabelFr: "N° TVA Intracom.",
                TaxIdLabelEn: "VAT Number",
                TradeRegisterLabelFr: "SIRET / RCS",
                TradeRegisterLabelEn: "SIRET / RCS",
                StandardVatRate: 20m,
                SuggestedVatRates: new[] { 0m, 5.5m, 10m, 20m },
                DefaultPaymentMethods: new[] { "Cash", "Carte Bancaire", "Virement Bancaire" }),

            new CountryProfile(
                Code: "US",
                NameFr: "États-Unis",
                NameEn: "United States",
                DefaultLanguage: "en",
                PrimaryCurrencyCode: "USD",
                SuggestedSecondaryCurrencyCode: "EUR",
                PhoneDialCode: "+1",
                DateFormat: "MM/dd/yyyy",
                TaxIdLabelFr: "TIN / EIN",
                TaxIdLabelEn: "TIN / EIN",
                TradeRegisterLabelFr: "State Reg.",
                TradeRegisterLabelEn: "State Reg.",
                StandardVatRate: 0m,
                SuggestedVatRates: new[] { 0m, 5m, 8.875m, 10m },
                DefaultPaymentMethods: new[] { "Cash", "Card", "Bank Transfer" })
        };

        public static IReadOnlyList<CountryProfile> AllCountries => Countries;
        public static IReadOnlyCollection<CurrencyInfo> AllCurrencies => Currencies.Values;

        public static CountryProfile GetCountry(string? code)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var found = Countries.FirstOrDefault(c => string.Equals(c.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
                if (found != null) return found;
            }
            return Countries[0]; // CD par défaut
        }

        public static CurrencyInfo GetCurrency(string? code)
        {
            if (!string.IsNullOrWhiteSpace(code) && Currencies.TryGetValue(code.Trim(), out var info))
                return info;

            return Currencies["USD"];
        }

        public static readonly HashSet<string> AllAllowedPaymentMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "Cash", "Espèces", "M-Pesa", "Airtel Money", "Orange Money", "Wave",
            "MTN MoMo", "Free Money", "Mobile Money", "Carte Bancaire", "Card",
            "Virement Bancaire", "Bank Transfer"
        };

        public static string FormatAmount(decimal amount, string? currencyCode, int? overrideDecimals = null, string? overrideSymbol = null, string language = "fr")
        {
            var currency = GetCurrency(currencyCode);
            var decimals = overrideDecimals ?? currency.Decimals;
            var symbol = !string.IsNullOrWhiteSpace(overrideSymbol) ? overrideSymbol! : currency.Symbol;

            var nfi = new NumberFormatInfo
            {
                NumberDecimalDigits = decimals,
                NumberGroupSeparator = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "," : "\u202F",
                NumberDecimalSeparator = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "." : ","
            };

            var formatted = decimal.Round(amount, decimals, MidpointRounding.AwayFromZero)
                .ToString($"N{decimals}", nfi);

            // Préfixer pour $, £, ₦, KSh, R en anglais ; suffixer proprement sinon
            if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) &&
                (symbol == "$" || symbol == "£" || symbol == "₦"))
            {
                return $"{symbol}{formatted}";
            }

            return $"{formatted} {symbol}";
        }

        public static decimal? ConvertPrimaryToSecondary(decimal primaryAmount, decimal? exchangeRateToSecondary, int secondaryDecimals)
        {
            if (!exchangeRateToSecondary.HasValue || exchangeRateToSecondary.Value <= 0m)
                return null;

            // Convention : 1 unité de devise secondaire (ex. 1 USD) = X unités de devise principale (ex. 2 850 CDF)
            return decimal.Round(primaryAmount / exchangeRateToSecondary.Value, secondaryDecimals, MidpointRounding.AwayFromZero);
        }
    }
}
