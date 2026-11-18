using System.Globalization;
using AdGestionHub.Models;

namespace AdGestionHub.Services;

public sealed class CommerceRuleException(string message) : Exception(message) { }

public static class Money
{
    public const string MaximumText = "100000000000";
    public const decimal MaximumAmount = 100_000_000_000m;
    public static IReadOnlyList<string> Currencies { get; } = ["CDF", "USD"];
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static bool IsMvpCurrency(string? code) => code is "CDF" or "USD";

    public static decimal Round(decimal value, string code) =>
        decimal.Round(value, CountryProfileCatalog.GetCurrency(code).Decimals, MidpointRounding.AwayFromZero);

    public static decimal Amount(decimal value, string code, bool allowZero = false)
    {
        if (value < 0 || value > MaximumAmount || value != Round(value, code) || (!allowZero && value == 0))
            throw new CommerceRuleException($"Montant invalide en {code}. Vérifiez le montant et le nombre de décimales.");
        return value;
    }

    public static decimal Convert(decimal value, string from, string to, decimal? cdfPerUsd)
    {
        if (from == to) return Round(value, to);
        if (!IsMvpCurrency(from) || !IsMvpCurrency(to) || cdfPerUsd is null or <= 0)
            throw new CommerceRuleException("Configurez le taux CDF/USD dans les paramètres pour utiliser l'autre devise.");
        return Round(from == "USD" ? value * cdfPerUsd.Value : value / cdfPerUsd.Value, to);
    }

    public static decimal? Rate(decimal? value)
    {
        if (value is null) return null;
        if (value <= 0 || value > 1_000_000_000m || decimal.Round(value.Value, 6) != value)
            throw new CommerceRuleException("Le taux doit être positif et comporter au plus six décimales (1 USD = X CDF).");
        return value;
    }

    public static string Display(decimal value, string code) =>
        $"{value.ToString($"N{CountryProfileCatalog.GetCurrency(code).Decimals}", French)} {code}";

    public static string RateLabel(decimal rate) => $"1 USD = {rate.ToString("0.######", French)} CDF";
    public static string Input(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";

    public static IReadOnlyList<string> AvailableCurrencies(StoreSettings settings) =>
        settings.CdfPerUsd is > 0 && IsMvpCurrency(settings.PrimaryCurrencyCode)
            ? Currencies : [settings.PrimaryCurrencyCode];
}

public static class PaymentMeans
{
    public static IReadOnlyList<string> BuiltIn { get; } =
        ["Espèces", "Mobile Money", "M-Pesa", "Airtel Money", "Orange Money", "Virement bancaire", "Carte bancaire", "Autre"];

    public static string[] Custom(string? values) => (values ?? "")
        .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Where(x => !BuiltIn.Contains(x, StringComparer.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> All(StoreSettings settings) => [.. BuiltIn, .. Custom(settings.CustomPaymentMethods)];

    public static string Validate(string? method, StoreSettings settings) =>
        All(settings).FirstOrDefault(x => x.Equals(method?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new CommerceRuleException("Sélectionnez un moyen de paiement valide.");

    public static string Display(string? method) => method switch
    {
        "Cash" => "Espèces",
        "Bank Transfer" or "Virement Bancaire" => "Virement bancaire",
        "Card" or "Carte Bancaire" => "Carte bancaire",
        null or "" => "Non précisé",
        _ => method
    };
}
