using AdGestionHub.Data;
using AdGestionHub.Middlewares;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.IO.Compression;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Les migrations versionnées ciblent SQL Server. SQLite sert au développement sur une base jetable.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("La chaîne de connexion DefaultConnection est obligatoire.");
var useSqlite = string.Equals(builder.Configuration["DatabaseProvider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
if (useSqlite && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("SQLite est réservé au développement. Configurez SQL Server pour le déploiement.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (useSqlite) options.UseSqlite(connectionString);
    else options.UseSqlServer(connectionString);
    // Le schéma SQL Server hérite de migrations historiques rédigées à la main.
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

// --- SERVICES MÉTIER & LOCALISATION ---
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAppLocalizer, AppLocalizer>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IBoutiqueService, BoutiqueService>();
builder.Services.AddScoped<ITenantContextService, TenantContextService>();
builder.Services.AddScoped<CommerceWriter>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<CommerceOperations>();
builder.Services.AddScoped<BusinessReportService>();
builder.Services.AddScoped<IPdfReportService, PdfReportService>();

// --- COMPRESSION GZIP / BROTLI ---
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "text/css",
        "application/javascript",
        "image/svg+xml",
        "font/woff2",
        "font/woff",
        "application/json"
    });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// --- HEALTH CHECKS ---
builder.Services.AddHealthChecks()
    .AddCheck<DbHealthCheck>("Database");

// --- IDENTITÉ & SÉCURITÉ DES COMPTES ---
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    options.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});

// --- COOKIES SÉCURISÉS ---
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "AdGestionHub.Auth";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.HeaderName = "RequestVerificationToken";
    options.SuppressXFrameOptionsHeader = builder.Environment.IsDevelopment();
});

// --- CORS ---
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "https://localhost:7221", "http://localhost:5216" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("StrictCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// --- CONTRÔLEURS, ANTI-CSRF GLOBAL & LIAISON DÉCIMALE SOUPLE ---
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.ModelBinderProviders.Insert(0, new FlexibleDecimalModelBinderProvider());
});

// --- SÉCURITÉ HSTS ---
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// --- POLITIQUES D'AUTORISATION ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ShopMember", policy => policy.RequireRole("Admin", "Employé"));
    options.AddPolicy("Dashboard", policy => policy.RequireRole("Admin", "Employé", "SuperAdmin"));

    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole("SuperAdmin"));

    options.AddPolicy("AdminOrSuperAdmin", policy =>
        policy.RequireRole("Admin", "SuperAdmin"));

    options.AddPolicy("Authenticated", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("ManageUsers", policy =>
        policy.RequireRole("Admin", "SuperAdmin"));

    options.AddPolicy("ManageShopSettings", policy =>
        policy.RequireRole("Admin", "SuperAdmin"));

    options.AddPolicy("ViewDashboard", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("ManageGlobalNotifications", policy =>
        policy.RequireRole("SuperAdmin"));

    options.AddPolicy("ViewLogs", policy =>
        policy.RequireRole("SuperAdmin"));
});

var app = builder.Build();

// Le MVP est en français (RDC), sans sélection de langue ni profil fiscal.
var french = new CultureInfo("fr-CD");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(french),
    SupportedCultures = [french],
    SupportedUICultures = [french],
    RequestCultureProviders = []
});

// Arrêt explicite si le schéma ou les rôles n'ont pas pu être initialisés.
await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();
    if (useSqlite)
    {
        await db.Database.EnsureCreatedAsync();
        // EnsureCreated ne met pas à jour un ancien fichier de développement.
        await db.Payments.AnyAsync();
    }
    else if (app.Configuration.GetValue("Database:ApplyMigrations", true))
        await db.Database.MigrateAsync();
    else if ((await db.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Appliquez les migrations SQL Server avant de démarrer l'application.");
    await DbInitializer.SeedAsync(services);
}

// --- Pipeline HTTP ---
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseResponseCompression();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var hasVersion = ctx.Context.Request.Query.ContainsKey("v")
                      || ctx.Context.Request.Path.StartsWithSegments("/lib");
        ctx.Context.Response.Headers["Cache-Control"] = hasVersion
            ? "public, max-age=31536000, immutable"
            : "public, max-age=3600";
    }
});

app.UseRouting();
app.UseCors("StrictCorsPolicy");
app.UseMiddleware<RateLimitingMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantActiveCheckMiddleware>();
app.UseAuthorization();

// --- ROUTES ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Welcome}/{id?}");
app.MapRazorPages();
app.MapHealthChecks("/health");

app.Run();

public class DbHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;
    public DbHealthCheck(ApplicationDbContext context) => _context = context;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy("OK")
            : HealthCheckResult.Unhealthy("Unavailable");
    }
}
