using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Services;

public sealed class BusinessReportService(ApplicationDbContext db)
{
    public async Task<BusinessReport> BuildAsync(int shop, StoreSettings settings, ReportPeriod period, string currency, bool finance)
    {
        var until = period.Until;
        var sales = await SalesUntil(shop, until, currency).Where(s => s.SaleDate >= period.Start)
            .Include(s => s.Items.Where(i => i.BoutiqueId == shop)).OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.Id).ToListAsync();
        var stock = await db.Products.AsNoTracking().Where(p => p.BoutiqueId == shop && !p.IsArchived).OrderBy(p => p.Name)
            .Select(p => new StockRow(p.Id, p.Name, p.Sku, p.StockQuantity, p.StockAlertThreshold, finance ? p.PurchasePrice : 0m)).ToListAsync();
        if (!finance) return new(period, currency, settings, sales, [], [], [], stock, []);

        var purchases = await PurchasesUntil(shop, until, currency).Where(p => p.RecordedAt >= period.Start)
            .OrderByDescending(p => p.RecordedAt).ThenByDescending(p => p.Id).ToListAsync();
        var expenses = await db.Expenses.AsNoTracking().Where(e => e.BoutiqueId == shop && e.CurrencyCode == currency
                && e.Date >= period.Start && e.Date < until).OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).ToListAsync();
        // A payment contributes cash on its own date, in the currency actually tendered (not the invoice currency).
        var payments = await db.Payments.AsNoTracking().Where(p => p.BoutiqueId == shop && p.CurrencyCode == currency
                && p.RecordedAt >= period.Start && p.RecordedAt < until).OrderByDescending(p => p.RecordedAt).ThenByDescending(p => p.Id).ToListAsync();
        return new(period, currency, settings, sales, purchases, expenses, payments, stock, await OutstandingAsync(shop, until, currency, includeSupplierHistory: true));
    }

    public async Task<IReadOnlyList<DocumentBalance>> OutstandingAsync(int shop, DateTime until, string? currency = null, bool includeSupplierHistory = false)
    {
        // Compute paid amounts up to the closing date; later payments must not rewrite a past balance.
        var sales = await SalesUntil(shop, until, currency).Where(s => s.FinalPrice > 0)
            .Include(s => s.Payments.Where(p => p.RecordedAt < until)).ToListAsync();
        var manual = await db.Debts.AsNoTracking().Where(d => d.BoutiqueId == shop && !d.IsArchived && !d.IsPaid
                && d.DateCreated < until && (currency == null || d.CurrencyCode == currency))
            .Include(d => d.Payments.Where(p => p.RecordedAt < until)).ToListAsync();
        var balances = sales.Select(s => new DocumentBalance(s.Id, s.Number, s.SaleDate, s.CustomerName ?? "Client de passage", s.CurrencyCode, s.FinalPrice, s.Paid, "Sale"))
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
