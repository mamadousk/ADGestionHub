using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Services;

public sealed class StockService(ApplicationDbContext db)
{
    public const int MaximumStock = 10_000_000;

    public async Task<int> ApplyAsync(int shop, Product product, int delta, string kind, string? reference, DateTime date,
        bool restoreArchived = false, string? actorUserId = null)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Un mouvement doit faire partie d'une transaction de stock.");
        if (product.BoutiqueId != shop || delta == 0 || delta < -MaximumStock || delta > MaximumStock)
            throw new CommerceRuleException("Mouvement de stock invalide.");

        var query = db.Products.Where(p => p.Id == product.Id && p.BoutiqueId == shop
            && (restoreArchived || !p.IsArchived)
            && p.StockQuantity >= -delta && p.StockQuantity <= MaximumStock - delta);
        var affected = restoreArchived
            ? await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.StockQuantity, p => p.StockQuantity + delta)
                .SetProperty(p => p.IsArchived, false))
            : await query.ExecuteUpdateAsync(setters => setters.SetProperty(p => p.StockQuantity, p => p.StockQuantity + delta));

        if (affected != 1)
            throw new CommerceRuleException(delta < 0
                ? $"Stock insuffisant ou indisponible pour « {product.Name} »."
                : $"Impossible d'ajouter au stock de « {product.Name} » (limite ou produit archivé).");

        var after = await db.Products.Where(p => p.Id == product.Id && p.BoutiqueId == shop)
            .Select(p => p.StockQuantity).SingleAsync();
        db.StockMovements.Add(new StockMovement
        {
            BoutiqueId = shop, ProductId = product.Id, Delta = delta, QuantityAfter = after,
            Kind = kind, Reference = reference, RecordedAt = date, CreatedByUserId = actorUserId
        });
        return after;
    }
}
