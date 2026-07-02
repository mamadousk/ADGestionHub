using AdGestionHub.Data;
using AdGestionHub.Middlewares;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Globalization;
using System.IO.Compression;

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

// --- COMPRESSION GZIP / BROTLI (RÉDUIT LA TAILLE DES RÉPONSES DE 70%) ---
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // Activer la compression même en HTTPS
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

// Configuration de la compression Brotli (meilleure que Gzip)
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest; // Vitesse vs compression : Fastest pour KINSHASA
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// --- HEALTH CHECKS ---
builder.Services.AddHealthChecks()
    .AddCheck<DbHealthCheck>("Database");

// --- IDENTITÉ ---
builder.Services.AddDefaultIdentity<AdGestionHub.Models.ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    options.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// --- COOKIES SÉCURISÉS ---
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("StrictCorsPolicy", policy =>
    {
        policy.WithOrigins("https://localhost:7221", "http://localhost:5216")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// --- CONTROLEURS ---
builder.Services.AddControllersWithViews();

// --- SÉCURITÉ HSTS ---
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

var app = builder.Build();

// --- Configuration de la Culture ---
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
    app.UseHsts();
}

// --- MIDDLEWARES (ORDRE OPTIMISÉ POUR LA VITESSE) ---
app.UseResponseCompression(); // ⚡ Compression des réponses (doit être le plus tôt possible)
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseCors("StrictCorsPolicy");

// --- FICHIERS STATIQUES AVEC CACHE NAVIGATEUR (1 AN) ---
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Mettre en cache les fichiers statiques pendant 1 an (pour les assets versionnés)
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000");
        ctx.Context.Response.Headers.Append("Expires", DateTime.UtcNow.AddYears(1).ToString("R"));
    }
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// --- HEALTH CHECKS ---
app.MapHealthChecks("/health");

// --- ROUTES ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Welcome}/{id?}");
app.MapRazorPages();

app.Run();

// --- Health Check personnalisé ---
public class DbHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;
    public DbHealthCheck(ApplicationDbContext context) => _context = context;

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