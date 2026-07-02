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
builder.Services.AddScoped<IBoutiqueService, BoutiqueService>();
builder.Services.AddHttpContextAccessor();

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

// =====================================================================
//  POLITIQUES D'AUTORISATION
// =====================================================================
builder.Services.AddAuthorization(options =>
{
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

// --- Culture ---
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

// --- Pipeline HTTP ---
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseCors("StrictCorsPolicy");

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000");
        ctx.Context.Response.Headers.Append("Expires", DateTime.UtcNow.AddYears(1).ToString("R"));
    }
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// --- ROUTES ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Welcome}/{id?}");
app.MapRazorPages();

// ============================================================
//  🚀 ENDPOINT DE RÉINITIALISATION DU MOT DE PASSE SUPERADMIN
//  (À SUPPRIMER APRÈS AVOIR RÉUSSI LA CONNEXION)
// ============================================================
app.MapGet("/resetpassword", async (UserManager<AdGestionHub.Models.ApplicationUser> userManager) =>
{
    const string email = "mamadousacko716@gmail.com";
    var user = await userManager.FindByEmailAsync(email);
    if (user == null)
        return Results.NotFound($"Utilisateur avec l'email {email} introuvable.");

    var token = await userManager.GeneratePasswordResetTokenAsync(user);
    var result = await userManager.ResetPasswordAsync(user, token, "Mamadousacko22@");
    if (result.Succeeded)
        return Results.Ok("✅ Mot de passe réinitialisé avec succès ! Vous pouvez maintenant vous connecter.");
    else
        return Results.BadRequest($"❌ Erreur : {string.Join(", ", result.Errors.Select(e => e.Description))}");
});

// --- HEALTH CHECKS ---
app.MapHealthChecks("/health");

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