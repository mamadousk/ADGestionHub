// ==================== FILE: Program.cs ====================
using AdGestionHub.Data;
using AdGestionHub.Middlewares;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rotativa.AspNetCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// --- SERVICES MÉTIER ---
builder.Services.AddScoped<IProfitService, ProfitService>();
builder.Services.AddScoped<IStockService, StockService>();

// --- CACHE ---
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICacheService, CacheService>();

// --- HEALTH CHECKS (sans package externe) ---
builder.Services.AddHealthChecks()
    .AddCheck<DbHealthCheck>("Database");

// --- IDENTITÉ AVEC SÉCURISATION RENFORCÉE ---
builder.Services.AddDefaultIdentity<AdGestionHub.Models.ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;

    // Paramètres de mot de passe sécurisés
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Verrouillage de compte (anti-brute force)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // Confirmation email et utilisateur
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// --- CONFIGURATION DES COOKIES (SÉCURITÉ) ---
builder.Services.ConfigureApplicationCookie(options =>
{
    // Le cookie ne sera accessible qu'en HTTPS
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    // Empêche l'accès au cookie via JavaScript (anti-XSS)
    options.Cookie.HttpOnly = true;

    // Politique SameSite : protège contre les attaques CSRF
    options.Cookie.SameSite = SameSiteMode.Lax;

    // Le cookie expire après 30 jours d'inactivité
    options.ExpireTimeSpan = TimeSpan.FromDays(30);

    // Renouvelle automatiquement le cookie à chaque requête
    options.SlidingExpiration = true;
});

// --- CONFIGURATION CORS (SÉCURITÉ) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("StrictCorsPolicy", policy =>
    {
        // En production, remplacez par votre domaine exact
        // Exemple : .WithOrigins("https://mon-domaine.com", "https://www.mon-domaine.com")
        policy.WithOrigins("https://localhost:7221", "http://localhost:5216") // Pour le développement
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Attention : ne pas utiliser AllowCredentials avec des origines génériques (*)
    });
});

builder.Services.AddControllersWithViews();

// --- POLITIQUE DES COOKIES (Consentement) ---
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    // Ce lambda détermine si le consentement de l'utilisateur est requis pour les cookies non essentiels.
    options.CheckConsentNeeded = context => true;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.Always;
});

var app = builder.Build();

// --- Configuration de la Culture (pour les nombres décimaux) ---
var supportedCultures = new[] { new CultureInfo("fr-FR") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("fr-FR"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

// --- Seed des rôles et du SuperAdmin ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbInitializer.SeedAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // HSTS : force le navigateur à utiliser HTTPS pendant 1 an
    app.UseHsts();
}

// --- MIDDLEWARES (ORDRE CRUCIAL POUR LA SÉCURITÉ) ---
// 1. Redirection HTTPS (le plus important)
app.UseHttpsRedirection();

// 2. Middleware de Security Headers (juste après HSTS, avant toute génération de contenu)
app.UseMiddleware<SecurityHeadersMiddleware>();

// 3. Gestion des erreurs (déjà placé)
app.UseMiddleware<ExceptionMiddleware>();

// 4. Rate Limiting (protection contre les abus)
app.UseMiddleware<RateLimitingMiddleware>();

// 5. CORS (doit être placé avant UseAuthorization)
app.UseCors("StrictCorsPolicy");

// 6. Fichiers statiques
app.UseStaticFiles();
// Configuration de Rotativa pour les PDF (après UseStaticFiles)
RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");
app.UseCookiePolicy();

// 7. Routage
app.UseRouting();

// 8. Authentification et Autorisation
app.UseAuthentication();
app.UseAuthorization();

// 9. Health Checks
app.MapHealthChecks("/health");

// 10. Controllers et Razor Pages
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Welcome}/{id?}");
app.MapRazorPages();

app.Run();

// --- Health Check personnalisé pour la base de données ---
public class DbHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;

    public DbHealthCheck(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy("La base de données est accessible.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("La base de données n'est pas accessible.", ex);
        }
    }
}