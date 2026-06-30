using AdGestionHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AdGestionHub.Models;

var builder = WebApplication.CreateBuilder(args);

// --- 1. SERVICES ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllersWithViews();

// CONFIGURATION IDENTITY : Passage à AddIdentity pour une meilleure gestion des rôles
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

builder.Services.AddRazorPages();

var app = builder.Build();

// --- 2. PIPELINE MIDDLEWARE ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Welcome}/{id?}");

// --- 3. INITIALISATION DES RÔLES ET DU SUPERADMIN ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    // Création des rôles s'ils n'existent pas
    string[] roleNames = { "SuperAdmin", "Admin", "Employé" };
    foreach (var roleName in roleNames)
    {
        if (!roleManager.RoleExistsAsync(roleName).Result)
        {
            roleManager.CreateAsync(new IdentityRole(roleName)).Wait();
        }
    }

    // Attribution automatique du rôle SuperAdmin à Mamadou
    var superAdminEmail = "mamadousacko716@gmail.com";
    var madi = userManager.FindByEmailAsync(superAdminEmail).Result;

    if (madi != null)
    {
        // Si l'utilisateur existe mais n'a pas encore le rôle
        if (!userManager.IsInRoleAsync(madi, "SuperAdmin").Result)
        {
            userManager.AddToRoleAsync(madi, "SuperAdmin").Wait();
        }
    }
}

// --- 4. CONFIGURATION EXTERNE ---
IWebHostEnvironment env = app.Environment;
Rotativa.AspNetCore.RotativaConfiguration.Setup(env.WebRootPath, "Rotativa");

// Ajoute ceci pour activer ton surveillant d'erreurs
app.UseMiddleware<AdGestionHub.Middlewares.ExceptionMiddleware>();

app.Run();