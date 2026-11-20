using AdGestionHub.Models;

namespace AdGestionHub.Services;

public sealed record ProfitSummary(decimal? Total, bool HasMissingCosts);

public static class SaleProfit
{
    public static decimal? ForSale(Sale sale)
    {
        if (sale.IsCancelled || sale.Items.Count == 0) return null;
        // The oldest imports can have a default zero UnitCost and no line snapshot. A zero
        // cost on a recorded line is valid; don't substitute today's catalogue price for missing costs.
        if (sale.Items.Any(line => line.Quantity <= 0 || line.UnitCost < 0m || (line.UnitCost == 0m
            && line.LineNet == 0m && line.LineTax == 0m && line.LineTotal == 0m && sale.CreatedByUserId == null)))
            return null;

        // FinalPrice already includes discounts. Historical VAT is not revenue; payments are separate.
        return sale.FinalPrice - sale.TaxAmount - sale.Items.Sum(line => line.UnitCost * line.Quantity);
    }

    public static ProfitSummary ForPeriod(IEnumerable<Sale> sales)
    {
        var total = 0m;
        var hasKnownProfit = false;
        var hasMissingCosts = false;
        foreach (var sale in sales)
        {
            if (sale.IsCancelled) continue;
            if (ForSale(sale) is decimal profit)
            {
                total += profit;
                hasKnownProfit = true;
            }
            else hasMissingCosts = true;
        }
        return new(hasKnownProfit || !hasMissingCosts ? total : null, hasMissingCosts);
    }
}
