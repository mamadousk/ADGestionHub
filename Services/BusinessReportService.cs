using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Services;

public sealed class BusinessReportService(ApplicationDbContext db)
{
    public async Task<BusinessReport> BuildAsync(int shop, StoreSettings settings, ReportPeriod period, string currency, bool finance,
        bool includeSaleItems = true, bool includeProfit = false)
    {
        var until = period.Until;
        var salesQuery = SalesUntil(shop, until, currency).Where(s => s.SaleDate >= period.Start);
        if (includeSaleItems || includeProfit) salesQuery = salesQuery.Include(s => s.Items.Where(i => i.BoutiqueId == shop));
        var sales = await salesQuery.OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.Id).ToListAsync();
        var stock = await db.Products.AsNoTracking().Where(p => p.BoutiqueId == shop && !p.IsArchived).OrderBy(p => p.Name)
            .Select(p => new StockRow(p.Id, p.Name, p.Sku, p.StockQuantity, p.StockAlertThreshold, finance ? p.PurchasePrice : 0m)).ToListAsync();
        var profit = includeProfit ? SaleProfit.ForPeriod(sales) : null;
        if (!finance) return new(period, currency, settings, sales, [], [], [], stock, []) { Profit = profit };

        var purchases = await PurchasesUntil(shop, until, currency).Where(p => p.RecordedAt >= period.Start)
            .OrderByDescending(p => p.RecordedAt).ThenByDescending(p => p.Id).ToListAsync();
        var expenses = await PeriodExpenses(shop, period).Where(e => e.CurrencyCode == currency).ToListAsync();
        // A payment contributes cash on its own date, in the currency actually tendered (not the invoice currency).
        var payments = await PeriodPayments(shop, period).Where(p => p.CurrencyCode == currency).ToListAsync();
        return new(period, currency, settings, sales, purchases, expenses, payments, stock, await OutstandingAsync(shop, until, currency, includeSupplierHistory: true))
            { Profit = profit };
    }

    // These are activity lists, not financial aggregates. They keep the original currency on
    // each document (and keep cancellations visible) rather than letting the totals filter hide an operation.
    public Task<List<Sale>> SaleActivityAsync(int shop, ReportPeriod period, int? limit, CancellationToken ct)
    {
        var until = period.Until;
        IQueryable<Sale> query = db.Sales.AsNoTracking().Where(s => s.BoutiqueId == shop && s.SaleDate >= period.Start && s.SaleDate < until)
            .OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.Id);
        if (limit.HasValue) query = query.Take(limit.Value);
        return query.ToListAsync(ct);
    }

    public async Task<PeriodOperations> ActivityAsync(int shop, ReportPeriod period, CancellationToken ct) => new(
        await SaleActivityAsync(shop, period, null, ct),
        await PeriodExpenses(shop, period).ToListAsync(ct),
        await PeriodPayments(shop, period).Where(p => p.PurchaseId == null).ToListAsync(ct));

    private IOrderedQueryable<Expense> PeriodExpenses(int shop, ReportPeriod period)
    {
        var until = period.Until;
        return db.Expenses.AsNoTracking().Where(e => e.BoutiqueId == shop && e.Date >= period.Start && e.Date < until)
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id);
    }

    private IOrderedQueryable<Payment> PeriodPayments(int shop, ReportPeriod period)
    {
        var until = period.Until;
        return db.Payments.AsNoTracking().Where(p => p.BoutiqueId == shop && p.RecordedAt >= period.Start && p.RecordedAt < until)
            .OrderByDescending(p => p.RecordedAt).ThenByDescending(p => p.Id);
    }

    public async Task<IReadOnlyList<DocumentBalance>> OutstandingAsync(int shop, DateTime until, string? currency = null, bool includeSupplierHistory = false)
    {
        // Compute paid amounts up to the closing date; later payments must not rewrite a past balance.
        var sales = await SalesUntil(shop, until, currency).Where(s => s.FinalPrice > 0)
            .Include(s => s.Payments.Where(p => p.RecordedAt < until)).ToListAsync();
        var manual = await db.Debts.AsNoTracking().Where(d => d.BoutiqueId == shop && !d.IsArchived && !d.IsPaid
                && d.DateCreated < until && (currency == null || d.CurrencyCode == currency))
            .Include(d => d.Payments.Where(p => p.RecordedAt < until)).ToListAsync();
        var balances = sales.Select(s => new DocumentBalance(s.Id, s.Number, s.SaleDate, s.CustomerName ?? "Client de passage", s.CurrencyCode, s.FinalPrice, s.Paid, "Sale") { CreatedByUserId = s.CreatedByUserId })
            .Concat(manual.Select(d => new DocumentBalance(d.Id, $"Créance #{d.Id}", d.DateCreated, d.ClientName, d.CurrencyCode, d.Amount, d.Paid, "Debt")));
        if (includeSupplierHistory)
        {
            var purchases = await PurchasesUntil(shop, until, currency).Where(p => p.Total > 0)
                .Include(p => p.Payments.Where(x => x.RecordedAt < until)).ToListAsync();
            balances = balances.Concat(purchases.Select(p => new DocumentBalance(p.Id, p.Number, p.RecordedAt, p.SupplierName, p.CurrencyCode, p.Total, p.Paid, "Purchase")));
        }
        return balances.Where(d => d.Balance > 0).OrderByDescending(d => d.Date).ThenByDescending(d => d.Id).ToList();
    }

    private IQueryable<Sale> SalesUntil(int shop, DateTime until, string? currency) => db.Sales.AsNoTracking()
        .Where(s => s.BoutiqueId == shop && s.SaleDate < until && (!s.IsCancelled || s.CancelledAt >= until)
            && (currency == null || s.CurrencyCode == currency));

    private IQueryable<Purchase> PurchasesUntil(int shop, DateTime until, string? currency) => db.Purchases.AsNoTracking()
        .Where(p => p.BoutiqueId == shop && p.RecordedAt < until && (currency == null || p.CurrencyCode == currency));
}
