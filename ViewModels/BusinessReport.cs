using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;

namespace AdGestionHub.Models;

public sealed record ReportPeriod(DateTime Start, DateTime End)
{
    public DateTime Until => End.AddDays(1);

    public static ReportPeriod Current(DateTime? start, DateTime? end)
    {
        var last = end?.Date ?? DateTime.Today;
        var first = start?.Date ?? new DateTime(last.Year, last.Month, 1);
        if (last > DateTime.Today || first.Year < 2000 || last < first || (last - first).TotalDays >= 366)
            throw new CommerceRuleException("Choisissez une période valide de 366 jours au maximum, sans date future.");
        return new(first, last);
    }
}

public sealed record StockRow(int Id, string Name, string? Sku, int Quantity, int AlertThreshold, decimal UnitCost)
{
    public decimal Value => Quantity * UnitCost;
    public bool IsLow => Quantity <= AlertThreshold;
}

public sealed record TopProduct(string Name, long Units);
public sealed record SalesPoint(string Label, decimal Total);

public sealed record BusinessReport(ReportPeriod Period, string Currency, StoreSettings Settings,
    IReadOnlyList<Sale> Sales, IReadOnlyList<Purchase> Purchases, IReadOnlyList<Expense> Expenses,
    IReadOnlyList<Payment> Payments, IReadOnlyList<StockRow> Stock, IReadOnlyList<DocumentBalance> Outstanding)
{
    public decimal Invoiced => Sales.Sum(s => s.FinalPrice);
    public decimal Revenue => Invoiced - Sales.Sum(s => s.TaxAmount);
    public decimal Purchased => Purchases.Sum(p => p.Total);
    public decimal Expensed => Expenses.Sum(e => e.Amount);
    public decimal Received => Payments.Where(p => p.PurchaseId == null).Sum(p => p.Amount);
    public decimal SalesReceipts => Payments.Where(p => p.SaleId != null).Sum(p => p.Amount);
    public decimal SupplierPayments => Payments.Where(p => p.PurchaseId != null).Sum(p => p.Amount);
    public decimal Receivables => Outstanding.Where(b => b.Kind != "Purchase").Sum(b => b.Balance);
    public bool HasSupplierHistory => Purchases.Count > 0 || Payments.Any(p => p.PurchaseId.HasValue) || Outstanding.Any(b => b.Kind == "Purchase");
    public decimal Payables => Outstanding.Where(b => b.Kind == "Purchase").Sum(b => b.Balance);
    public long StockUnits => Stock.Sum(p => (long)p.Quantity);
    public decimal StockValue => Stock.Sum(p => p.Value);

    public IEnumerable<TopProduct> TopProducts => Sales.SelectMany(s => s.Items)
        .GroupBy(i => new { i.ProductId, Name = i.ProductId == null ? i.ProductName : null })
        .Select(g => new TopProduct(g.First().ProductName, g.Sum(i => (long)i.Quantity)))
        .OrderByDescending(g => g.Units).ThenBy(g => g.Name);

    public List<SalesPoint> Trend()
    {
        var points = new List<SalesPoint>();
        var numberOfDays = (Period.End - Period.Start).Days + 1;
        var step = numberOfDays <= 14 ? 1 : numberOfDays <= 84 ? 7 : (numberOfDays + 11) / 12;
        var days = Sales.GroupBy(s => s.SaleDate.Date).ToDictionary(g => g.Key, g => g.Sum(s => s.FinalPrice));
        for (var date = Period.Start; date <= Period.End; date = date.AddDays(step))
        {
            var total = 0m;
            var bound = date.AddDays(step);
            for (var day = date; day < bound && day <= Period.End; day = day.AddDays(1))
                total += days.GetValueOrDefault(day);
            points.Add(new SalesPoint(date.ToString("dd/MM"), total));
        }
        return points;
    }
}
