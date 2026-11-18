using System.Globalization;
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Services;

public sealed class CommerceOperations(ApplicationDbContext db, CommerceWriter writer, StockService stock)
{
    public const int MaxLines = 50;

    public Task<Sale> SellAsync(int shop, SaleInput input, bool canDiscount) => writer.ExecuteAsync(shop, async () =>
    {
        var settings = await writer.SettingsAsync(shop);
        var code = DocumentCurrency(input.CurrencyCode, settings);
        var products = await ProductsAsync(shop, input.Items);
        var client = await ClientAsync(shop, input.ClientId);
        var now = DateTime.Now;
        var sale = NewSale(shop, code, settings.CdfPerUsd, client?.Id, client?.Name ?? Clean(input.CustomerName), now);
        foreach (var line in input.Items)
        {
            var product = products[line.ProductId];
            var price = SalePrice(product, code, settings);
            sale.Items.Add(new SaleItem
            {
                BoutiqueId = shop, ProductId = product.Id, ProductName = product.Name, Quantity = line.Quantity,
                UnitPrice = price,
                UnitCost = Money.Amount(Money.Convert(product.PurchasePrice, settings.PrimaryCurrencyCode, code, settings.CdfPerUsd), code, allowZero: true),
                LineNet = price * line.Quantity, LineTotal = price * line.Quantity
            });
        }

        sale.SubtotalNet = Money.Amount(sale.Items.Sum(i => i.LineNet), code, allowZero: true);
        sale.Discount = Money.Amount(input.Discount, code, allowZero: true);
        if (sale.Discount > 0 && !canDiscount)
            throw new CommerceRuleException("Seul l'administrateur peut appliquer une remise.");
        if (sale.Discount > sale.SubtotalNet)
            throw new CommerceRuleException("La remise ne peut pas dépasser le sous-total.");
        sale.FinalPrice = sale.SubtotalNet - sale.Discount;
        VerifyTotal(input, sale.FinalPrice);
        var initial = InitialPayment(input, settings, shop, code, sale.FinalPrice, now);
        if ((initial?.AppliedAmount ?? 0) < sale.FinalPrice && sale.CustomerName == null)
            throw new CommerceRuleException("Sélectionnez un client ou saisissez son nom pour suivre le montant restant.");
        if (initial != null) sale.Payments.Add(initial);

        await SaveSaleAsync(sale, settings, products);
        return sale;
    });

    public Task<bool> CancelSaleAsync(int shop, int id, string? reason) => writer.ExecuteAsync(shop, async () =>
    {
        var sale = await db.Sales.Include(s => s.Items.Where(i => i.BoutiqueId == shop)).Include(s => s.Payments)
            .SingleOrDefaultAsync(s => s.Id == id && s.BoutiqueId == shop) ?? throw new CommerceNotFoundException();
        if (sale.IsCancelled) return false;
        if (sale.Payments.Count != 0)
            throw new CommerceRuleException("Cette vente comporte un règlement. Son annulation et son remboursement ne sont pas gérés en V1.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 300)
            throw new CommerceRuleException("Indiquez un motif d'annulation (300 caractères au maximum).");

        var now = DateTime.Now;
        var ids = sale.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).ToArray();
        var products = await db.Products.AsNoTracking().Where(p => p.BoutiqueId == shop && ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        foreach (var line in sale.Items.OrderBy(i => i.ProductId))
        {
            // Old records can lack a product reference (the product was deleted before V1).
            if (line.ProductId is int productId && products.TryGetValue(productId, out var product))
                await stock.ApplyAsync(shop, product, line.Quantity, "Annulation vente", sale.Number, now, restoreArchived: true);
        }

        sale.IsCancelled = true;
        sale.CancelledAt = now;
        sale.CancellationReason = reason.Trim();
        writer.Audit(shop, $"Annulation de la vente {sale.Number}", sale.CancellationReason);
        return true;
    });

    public Task<Payment> RecordPaymentAsync(int shop, string target, int? id, PaymentInput input) => writer.ExecuteAsync(shop, async () =>
    {
        var settings = await writer.SettingsAsync(shop);
        var description = Clean(input.Description);
        var now = DateTime.Now;
        var (number, currency, balance) = target switch
        {
            "Sale" => await SaleBalanceAsync(shop, id),
            "Debt" => await OldBalanceAsync(shop, id),
            "Receipt" when description != null && Money.IsMvpCurrency(input.CurrencyCode) && Money.AvailableCurrencies(settings).Contains(input.CurrencyCode) =>
                (description, input.CurrencyCode, Money.MaximumAmount),
            "Receipt" => throw new CommerceRuleException("Indiquez le motif de l'encaissement et une devise valide."),
            _ => throw new CommerceNotFoundException()
        };
        if (!Money.IsMvpCurrency(currency) || !Money.IsMvpCurrency(input.CurrencyCode))
            throw new CommerceRuleException("Seuls les règlements en CDF et USD sont disponibles dans cette version.");
        var amount = Money.Amount(input.Amount, input.CurrencyCode);
        var applied = Money.Convert(amount, input.CurrencyCode, currency, settings.CdfPerUsd);
        if (applied <= 0 || applied > balance)
            throw new CommerceRuleException($"Le règlement doit être positif et ne pas dépasser le reste à régler : {Money.Display(balance, currency)}.");

        var payment = NewPayment(shop, settings, input.CurrencyCode, amount, currency, applied, input.Method, now);
        payment.Description = description;
        payment.SaleId = target == "Sale" ? id : null;
        payment.DebtId = target == "Debt" ? id : null;
        db.Payments.Add(payment);
        writer.Audit(shop, $"Règlement enregistré : {number}", $"{Money.Display(amount, input.CurrencyCode)} · {payment.Method}");
        return payment;
    });

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<Client?> ClientAsync(int shop, int? id) => !id.HasValue ? null :
        await db.Clients.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.BoutiqueId == shop)
        ?? throw new CommerceRuleException("Sélectionnez un client de votre entreprise.");

    private async Task<Dictionary<int, Product>> ProductsAsync(int shop, List<TradeLineInput> lines)
    {
        if (lines.Count is < 1 or > MaxLines || lines.Any(l => l.ProductId < 1 || l.Quantity is < 1 or > 100_000))
            throw new CommerceRuleException($"Ajoutez entre 1 et {MaxLines} produits avec des quantités valides.");
        var ids = lines.Select(x => x.ProductId).Distinct().ToArray();
        // Repeated products are allowed (e.g. a purchase with separate supplier prices). Stock updates are cumulative.
        var products = await db.Products.AsNoTracking()
            .Where(p => p.BoutiqueId == shop && !p.IsArchived && ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        if (products.Count != ids.Length)
            throw new CommerceRuleException("Un des produits n'est plus disponible dans le catalogue de votre entreprise.");
        return products;
    }

    private static void VerifyTotal(TradeInput input, decimal final)
    {
        if (input.ExpectedTotal.HasValue && input.ExpectedTotal.Value != final)
            throw new CommerceRuleException("Le total a changé (prix ou taux). Vérifiez le montant actualisé avant de confirmer.");
    }

    private static string DocumentCurrency(string code, StoreSettings settings)
    {
        if (!Money.IsMvpCurrency(code)) throw new CommerceRuleException("Choisissez le CDF ou l'USD.");
        if (!Money.AvailableCurrencies(settings).Contains(code))
            throw new CommerceRuleException("Configurez le taux CDF/USD pour enregistrer dans cette devise.");
        return code;
    }

    private static decimal SalePrice(Product product, string code, StoreSettings settings)
    {
        var price = Money.Convert(product.SalePrice, settings.PrimaryCurrencyCode, code, settings.CdfPerUsd);
        if (product.SalePrice > 0 && price == 0)
            throw new CommerceRuleException($"Le prix de « {product.Name} » est trop faible pour être converti en {code}.");
        return Money.Amount(price, code, allowZero: true);
    }

    private static Sale NewSale(int shop, string code, decimal? rate, int? clientId, string? customer, DateTime now) => new()
    {
        BoutiqueId = shop, SaleDate = now, ClientId = clientId, CustomerName = customer, DocumentType = "Invoice",
        CurrencyCode = code, CurrencySymbol = CountryProfileCatalog.GetCurrency(code).Symbol,
        CurrencyDecimals = CountryProfileCatalog.GetCurrency(code).Decimals, CdfPerUsd = rate
    };

    private async Task SaveSaleAsync(Sale sale, StoreSettings settings, Dictionary<int, Product> products)
    {
        var (number, next) = await AllocateNumberAsync("FAC-", settings.NextInvoiceNumber,
            db.Sales.Where(s => s.BoutiqueId == sale.BoutiqueId).Select(s => s.DocumentNumber));
        sale.DocumentNumber = number;
        settings.NextInvoiceNumber = next;
        var warnings = new HashSet<int>();
        foreach (var line in sale.Items.OrderBy(i => i.ProductId))
        {
            var product = products[line.ProductId!.Value];
            var remaining = await stock.ApplyAsync(sale.BoutiqueId, product, -line.Quantity, "Vente", sale.Number, sale.SaleDate);
            if (remaining <= product.StockAlertThreshold) warnings.Add(product.Id);
        }
        sale.StockWarnings = warnings.Count;
        db.Sales.Add(sale);
        writer.Audit(sale.BoutiqueId, $"Vente {sale.Number} enregistrée",
            $"Total : {Money.Display(sale.FinalPrice, sale.CurrencyCode)} · Réglé : {Money.Display(sale.Paid, sale.CurrencyCode)}");
    }

    private static async Task<(string Number, int Next)> AllocateNumberAsync(string prefix, int counter, IQueryable<string> history)
    {
        // The tenant write lock prevents two requests from allocating the same number.
        // Recover from missing or stale settings without renumbering legacy documents.
        var next = (long)Math.Max(1, counter);
        var candidate = prefix + next.ToString("D5", CultureInfo.InvariantCulture);
        if (next == 1 || await history.AnyAsync(number => number == candidate))
        {
            var references = await history.Where(number => number.StartsWith(prefix)).ToListAsync();
            foreach (var reference in references)
                if (int.TryParse(reference.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal))
                    next = Math.Max(next, (long)ordinal + 1);
        }
        if (next >= int.MaxValue) throw new CommerceRuleException("La séquence de numérotation est épuisée. Contactez l'assistance.");
        return (prefix + next.ToString("D5", CultureInfo.InvariantCulture), (int)next + 1);
    }

    private static Payment? InitialPayment(PayableInput input, StoreSettings settings, int shop, string code, decimal total, DateTime now)
    {
        var amount = input.PaymentMode switch
        {
            "Full" => total,
            "Unpaid" => 0m,
            "Partial" when input.InitialPayment is decimal partial && partial > 0 && partial < total => partial,
            "Partial" => throw new CommerceRuleException("Un règlement partiel doit être supérieur à zéro et inférieur au total."),
            _ => throw new CommerceRuleException("Choisissez l'état du règlement.")
        };
        return amount == 0 ? null : NewPayment(shop, settings, code, Money.Amount(amount, code), code, amount, input.PaymentMethod, now);
    }

    private static Payment NewPayment(int shop, StoreSettings settings, string currency, decimal amount, string appliedCurrency,
        decimal applied, string? method, DateTime now) => new()
    {
        BoutiqueId = shop, CurrencyCode = currency, Amount = amount, AppliedCurrencyCode = appliedCurrency, AppliedAmount = applied,
        Method = PaymentMeans.Validate(method, settings), CdfPerUsd = settings.CdfPerUsd, RecordedAt = now
    };

    private async Task<(string, string, decimal)> SaleBalanceAsync(int shop, int? id)
    {
        var sale = await db.Sales.AsNoTracking().Include(s => s.Payments).SingleOrDefaultAsync(s => s.BoutiqueId == shop && s.Id == id)
            ?? throw new CommerceNotFoundException();
        return (sale.Number, sale.CurrencyCode, sale.Balance);
    }

    private async Task<(string, string, decimal)> OldBalanceAsync(int shop, int? id)
    {
        var debt = await db.Debts.AsNoTracking().Include(s => s.Payments).SingleOrDefaultAsync(s => s.BoutiqueId == shop && s.Id == id && !s.IsArchived)
            ?? throw new CommerceNotFoundException();
        return ($"Créance manuelle #{debt.Id}", debt.CurrencyCode, debt.Balance);
    }
}
