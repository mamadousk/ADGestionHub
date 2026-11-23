using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Services;

/// <summary>Vérifie qu'un compte peut ouvrir une session (boutique active, etc.).</summary>
public sealed class AccountAccessGuard
{
    private readonly ApplicationDbContext _db;

    public AccountAccessGuard(ApplicationDbContext db) => _db = db;

    public async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        if (!user.BoutiqueId.HasValue) return true;

        var boutique = await _db.Boutiques
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == user.BoutiqueId.Value);

        return boutique == null || boutique.IsActive;
    }
}
