using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Services;

public sealed class CommerceNotFoundException : Exception { }

public sealed class CommerceWriter(ApplicationDbContext db, IHttpContextAccessor http)
{
    public async Task<T> ExecuteAsync<T>(int shop, Func<Task<T>> write)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            // Lock the tenant row before reading amounts or stock. Payment, cancellation,
            // configuration and onboarding share the same lock, including across app instances.
            var locked = await db.Boutiques.Where(b => b.Id == shop && b.IsActive)
                .ExecuteUpdateAsync(update => update.SetProperty(b => b.IsActive, b => b.IsActive));
            if (locked != 1) throw new CommerceNotFoundException();

            var result = await write();
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            // A form can reload the catalog after a rejected write in the same request.
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<StoreSettings> SettingsAsync(int shop)
    {
        var settings = await db.StoreSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(s => s.BoutiqueId == shop);
        if (settings != null) return settings;

        var boutique = await db.Boutiques.AsNoTracking().SingleAsync(b => b.Id == shop);
        settings = DefaultSettings(boutique);
        db.StoreSettings.Add(settings);
        return settings;
    }

    public static StoreSettings DefaultSettings(Boutique? boutique) => new()
    {
        BoutiqueId = boutique?.Id ?? 0,
        StoreName = boutique?.Nom ?? "AD Gestion Hub",
        Address = boutique?.Adresse,
        Phone = boutique?.Telephone,
        ContactEmail = boutique?.Email,
        FooterMessage = "Merci de votre confiance."
    };

    public void Audit(int shop, string message, string? details = null) => db.SystemLogs.Add(new SystemLog
    {
        BoutiqueId = shop,
        DateOccurred = DateTime.Now,
        UserEmail = http.HttpContext?.User.Identity?.Name ?? "Système",
        ErrorMessage = $"[AUDIT] {message}",
        StackTrace = details
    });
}
