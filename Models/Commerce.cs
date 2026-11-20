using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdGestionHub.Models;

public sealed class Supplier
{
    public int Id { get; set; }
    public int BoutiqueId { get; set; }
    [MaxLength(150)] public string Name { get; set; } = "";
    [MaxLength(40)] public string? Phone { get; set; }
    [MaxLength(250)] public string? Address { get; set; }
    [MaxLength(256)] public string? Email { get; set; }
}

public sealed class Purchase
{
    public int Id { get; set; }
    public int BoutiqueId { get; set; }
    public int SupplierId { get; set; }
    [MaxLength(150)] public string SupplierName { get; set; } = "";
    [MaxLength(100)] public string? SupplierReference { get; set; }
    [MaxLength(10)] public string CurrencyCode { get; set; } = "CDF";
    public decimal? CdfPerUsd { get; set; }
    public decimal Total { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public List<PurchaseItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    [NotMapped] public string Number => $"ACH-{Id:D5}";
    [NotMapped] public decimal Paid => Payments.Sum(p => p.AppliedAmount);
    [NotMapped] public decimal Balance => Total - Paid;
}

public sealed class PurchaseItem
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public int ProductId { get; set; }
    [MaxLength(200)] public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    [NotMapped] public decimal Total => UnitPrice * Quantity;
}

/// <summary>An actual cash movement, distinct from the amount invoiced.</summary>
public sealed class Payment
{
    public int Id { get; set; }
    public int BoutiqueId { get; set; }
    public int? SaleId { get; set; }
    public int? PurchaseId { get; set; }
    public int? DebtId { get; set; }
    [MaxLength(450)] public string? CreatedByUserId { get; set; }
    [MaxLength(250)] public string? Description { get; set; }
    [MaxLength(50)] public string Method { get; set; } = "";
    [MaxLength(10)] public string CurrencyCode { get; set; } = "CDF";
    [MaxLength(10)] public string AppliedCurrencyCode { get; set; } = "CDF";
    public decimal Amount { get; set; }
    public decimal AppliedAmount { get; set; }
    public decimal? CdfPerUsd { get; set; }
    public DateTime RecordedAt { get; set; }
}

public sealed class StockMovement
{
    public int Id { get; set; }
    public int BoutiqueId { get; set; }
    public int ProductId { get; set; }
    [MaxLength(450)] public string? CreatedByUserId { get; set; }
    [MaxLength(24)] public string Kind { get; set; } = "";
    [MaxLength(250)] public string? Reference { get; set; }
    public int Delta { get; set; }
    public int QuantityAfter { get; set; }
    public DateTime RecordedAt { get; set; }
}
