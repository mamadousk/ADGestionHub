using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Services;

// Persisted bits: never reuse an old value for a different feature.
[Flags]
public enum StaffPermission
{
    None = 0,
    Sell = 1 << 0,
    SalesHistory = 1 << 1,
    Catalogue = 1 << 2,
    Clients = 1 << 3,
    SalePayments = 1 << 4,
    Expenses = 1 << 5,
    Dashboard = 1 << 6,
    Reports = 1 << 7
}

public sealed record StaffFeature(string Code, StaffPermission Permission, string Name, string Description, string Icon, bool Sensitive = false);
public sealed record StaffProfile(string Name, string Description, string Icon, StaffPermission Permissions);

public static class StaffAccessRules
{
    public const string EmployeeRole = "Employé";
    public const string Sell = "staff.sale.create";
    public const string SalesHistory = "staff.sale.history";
    public const string SaleDocuments = "staff.sale.documents";
    public const string Catalogue = "staff.catalogue";
    public const string Clients = "staff.clients";
    public const string SalePayments = "staff.sale.payments";
    public const string Expenses = "staff.expenses";
    public const string Dashboard = "staff.dashboard";
    public const string Reports = "staff.reports";

    public const StaffPermission Cashier = StaffPermission.Sell;
    public const StaffPermission SalesAndStock = Cashier | StaffPermission.Catalogue | StaffPermission.Clients;
    public const StaffPermission SalesLead = SalesAndStock | StaffPermission.SalesHistory | StaffPermission.SalePayments | StaffPermission.Dashboard;
    // Frozen to the original employee access; future preset changes must not alter migrated accounts.
    public const StaffPermission Legacy = StaffPermission.Sell | StaffPermission.SalesHistory | StaffPermission.Catalogue
        | StaffPermission.Clients | StaffPermission.SalePayments | StaffPermission.Expenses | StaffPermission.Dashboard;
    public const StaffPermission All = Legacy | StaffPermission.Reports;

    public static IReadOnlyList<StaffFeature> Features { get; } = Array.AsReadOnly(new StaffFeature[]
    {
        new(Sell, StaffPermission.Sell, "Réaliser des ventes", "Utiliser la caisse et imprimer les reçus de ses propres ventes.", "shopping-bag"),
        new(SalesHistory, StaffPermission.SalesHistory, "Historique des ventes", "Consulter les ventes et reçus de toute l’entreprise, avec leurs montants.", "receipt", true),
        new(Catalogue, StaffPermission.Catalogue, "Catalogue et stock", "Consulter les produits, les quantités et les mouvements, sans prix d’achat.", "package"),
        new(Clients, StaffPermission.Clients, "Répertoire clients", "Consulter, créer et modifier les fiches clients, et les sélectionner en caisse.", "users"),
        new(SalePayments, StaffPermission.SalePayments, "Règlements sur ventes", "Enregistrer un versement après la vente. Nécessite l’historique des ventes.", "wallet"),
        new(Expenses, StaffPermission.Expenses, "Dépenses", "Saisir et consulter les dépenses, sans les corriger ni les supprimer.", "wallet", true),
        new(Dashboard, StaffPermission.Dashboard, "Tableau de bord", "Voir le chiffre d’affaires et les tendances des ventes de l’entreprise.", "layout-dashboard", true),
        new(Reports, StaffPermission.Reports, "Rapports et exports", "Consulter et exporter les chiffres, dont les dépenses, créances et coûts de stock.", "bar-chart-3", true)
    });

    public static IReadOnlyList<StaffProfile> Profiles { get; } = Array.AsReadOnly(new StaffProfile[]
    {
        new("Caisse uniquement", "Ventes et reçus personnels, sans historique global ni chiffre d’affaires.", "shopping-bag", Cashier),
        new("Vente et stock", "La caisse, le catalogue en lecture seule et le répertoire clients.", "package", SalesAndStock),
        new("Responsable des ventes", "Ventes, clients, stock, règlements et chiffre d’affaires.", "bar-chart-3", SalesLead)
    });

    public static bool Has(this StaffPermission granted, StaffPermission required) => (granted & required) == required;

    public static IEnumerable<string> Codes(StaffPermission permissions) => Features.Where(feature => permissions.Has(feature.Permission)).Select(feature => feature.Code);

    public static string ProfileName(StaffPermission permissions) => permissions switch
    {
        StaffPermission.None => "Aucun accès métier",
        Legacy => "Équipe polyvalente",
        _ => Profiles.FirstOrDefault(profile => profile.Permissions == permissions)?.Name ?? "Sur mesure"
    };

    // NULL is reserved for accounts created before configurable access. An explicit zero never falls back.
    public static StaffPermission FromStored(int? mask)
    {
        if (mask == null) return Legacy;
        var permissions = (StaffPermission)mask.Value;
        return IsValid(permissions) ? permissions : StaffPermission.None;
    }

    public static bool TryParse(IReadOnlyList<string>? codes, out StaffPermission permissions)
    {
        permissions = StaffPermission.None;
        if (codes == null || codes.Count > Features.Count) return false;
        foreach (var code in codes)
        {
            var feature = Features.FirstOrDefault(feature => feature.Code == code);
            if (feature == null || permissions.Has(feature.Permission)) return false;
            permissions |= feature.Permission;
        }
        return IsValid(permissions);
    }

    public static void AddPolicies(AuthorizationOptions options)
    {
        foreach (var feature in Features)
        {
            options.AddPolicy(feature.Code, policy => policy.RequireRole("Admin", EmployeeRole)
                .AddRequirements(new StaffAccessRequirement(feature.Permission)));
        }
        options.AddPolicy(SaleDocuments, policy => policy.RequireRole("Admin", EmployeeRole)
            .AddRequirements(new StaffAccessRequirement(StaffPermission.Sell | StaffPermission.SalesHistory)));
    }

    private static bool IsValid(StaffPermission permissions) => (permissions & ~All) == 0
        && (!permissions.Has(StaffPermission.SalePayments) || permissions.Has(StaffPermission.SalesHistory));
}

// Scoped to one HTTP request. Never use an access mask from a sign-in cookie or from form data.
public sealed class StaffAccess(ApplicationDbContext db, IBoutiqueService boutiques, IHttpContextAccessor http, UserManager<ApplicationUser> users)
{
    private Task<StaffPermission>? current;

    public Task<StaffPermission> CurrentAsync() => current ??= LoadAsync();

    private async Task<StaffPermission> LoadAsync()
    {
        var context = http.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true || context.User.IsInRole("SuperAdmin")) return StaffPermission.None;
        var admin = context.User.IsInRole("Admin");
        if (!admin && !context.User.IsInRole(StaffAccessRules.EmployeeRole)) return StaffPermission.None;
        if (await boutiques.GetCurrentBoutiqueAsync() is not { IsActive: true } shop) return StaffPermission.None;
        if (admin) return StaffAccessRules.All;
        var userId = users.GetUserId(context.User);
        var member = await db.Users.AsNoTracking().Where(user => user.Id == userId && user.BoutiqueId == shop.Id)
            .Select(user => new { user.StaffAccessMask }).SingleOrDefaultAsync(context.RequestAborted);
        return member == null ? StaffPermission.None : StaffAccessRules.FromStored(member.StaffAccessMask);
    }
}

public sealed record StaffAccessRequirement(StaffPermission AnyOf) : IAuthorizationRequirement;

public sealed class StaffAccessHandler(StaffAccess access) : AuthorizationHandler<StaffAccessRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, StaffAccessRequirement requirement)
    {
        if (((await access.CurrentAsync()) & requirement.AnyOf) != StaffPermission.None)
            context.Succeed(requirement);
    }
}
