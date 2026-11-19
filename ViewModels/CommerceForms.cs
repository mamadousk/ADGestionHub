using System.ComponentModel.DataAnnotations;
using AdGestionHub.Services;

namespace AdGestionHub.Models.ViewModels;

public sealed class ContactInput
{
    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [StringLength(150)] public string Name { get; set; } = "";
    [StringLength(40)] public string? Phone { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [EmailAddress(ErrorMessage = "Adresse e-mail invalide.")]
    [StringLength(256)] public string? Email { get; set; }
}

public sealed class TradeLineInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Sélectionnez un produit pour chaque ligne.")]
    public int ProductId { get; set; }
    [Range(1, 100_000, ErrorMessage = "La quantité doit être comprise entre 1 et 100 000.")]
    public int Quantity { get; set; } = 1;
}

public class TradeInput
{
    [Required] [StringLength(10)] public string CurrencyCode { get; set; } = "CDF";
    public decimal? ExpectedTotal { get; set; }
    public List<TradeLineInput> Items { get; set; } = [new()];
}

public class PayableInput : TradeInput
{
    [Required] [StringLength(10)] public string PaymentMode { get; set; } = "Full";
    [Range(typeof(decimal), "0", Money.MaximumText, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "Le montant réglé est invalide.")]
    public decimal? InitialPayment { get; set; }
    [StringLength(50)] public string? PaymentMethod { get; set; }
}

public sealed class SaleInput : PayableInput
{
    public int? ClientId { get; set; }
    [StringLength(150)] public string? CustomerName { get; set; }
    [Range(typeof(decimal), "0", Money.MaximumText, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "La remise est invalide.")]
    public decimal Discount { get; set; }
}

public sealed class PaymentInput
{
    [Range(typeof(decimal), "0.01", Money.MaximumText, ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "Saisissez un montant supérieur à zéro.")]
    public decimal Amount { get; set; }
    [Required] [StringLength(10)] public string CurrencyCode { get; set; } = "CDF";
    [Required] [StringLength(50)] public string Method { get; set; } = "Espèces";
    [StringLength(250)] public string? Description { get; set; }
}

public sealed class StockAdjustmentInput
{
    [Range(1, 100_000)] public int Quantity { get; set; } = 1;
    public bool IsEntry { get; set; } = true;
    [Required(ErrorMessage = "Indiquez le motif du mouvement.")]
    [StringLength(250)] public string Reason { get; set; } = "";
}

public sealed class ShopSettingsInput
{
    [Required(ErrorMessage = "Le nom de l'entreprise est obligatoire.")]
    [StringLength(150)] public string StoreName { get; set; } = "";
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(40)] public string? Phone { get; set; }
    [EmailAddress(ErrorMessage = "Adresse e-mail invalide.")] [StringLength(256)] public string? ContactEmail { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    public IFormFile? Logo { get; set; }
    public bool RemoveLogo { get; set; }
    [StringLength(300)] public string? FooterMessage { get; set; }
    [Required] [StringLength(10)] public string PrimaryCurrencyCode { get; set; } = "CDF";
    [Range(typeof(decimal), "0.000001", "1000000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "Saisissez un taux strictement positif.")]
    public decimal? CdfPerUsd { get; set; }
    [StringLength(600)] public string? CustomPaymentMethods { get; set; }
}

public sealed record BusinessHistory<T>(T Contact, IReadOnlyList<DocumentBalance> Documents);

public sealed record DocumentBalance(int Id, string Number, DateTime Date, string Contact, string Currency,
    decimal Total, decimal Paid, string Kind)
{
    public decimal Balance => Total - Paid;
}

public sealed record NewPaymentForm(string Action, int Id, string Currency, decimal Balance);

public sealed record CatalogPick(int Id, string Name, string? Sku, string? Category, int Stock, int StockAlertThreshold,
    IReadOnlyDictionary<string, decimal> Prices);
public sealed record ContactPick(int Id, string Name);
public sealed record TradeLineRow(TradeLineInput Line, int Index);
public sealed record CommercialLine(string Name, int Quantity, decimal Price, decimal Total);
public sealed record CommercialTable(IEnumerable<CommercialLine> Lines, string Currency);
public sealed record ContactRow(int Id, string Name, string? Phone, string? Email, string? Address);
