using AdGestionHub.Models;
using Microsoft.AspNetCore.Identity;

namespace AdGestionHub.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

            // 1. Création des rôles standards
            string[] roleNames = { "Admin", "Employé", "SuperAdmin" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var role = await roleManager.CreateAsync(new IdentityRole(roleName));
                    if (!role.Succeeded && !await roleManager.RoleExistsAsync(roleName))
                        throw new InvalidOperationException($"Le rôle {roleName} n'a pas pu être initialisé.");
                }
            }

            // 2. Création du SuperAdmin uniquement si ses identifiants sont fournis par la configuration
            //    (variables d'environnement SuperAdmin__Email / SuperAdmin__Password, ou User Secrets en développement).
            var superAdminEmail = configuration["SuperAdmin:Email"];
            var superAdminPassword = configuration["SuperAdmin:Password"];

            if (string.IsNullOrWhiteSpace(superAdminEmail) || string.IsNullOrWhiteSpace(superAdminPassword))
            {
                logger.LogInformation(
                    "SuperAdmin:Email / SuperAdmin:Password non définis dans la configuration : aucun nouveau compte SuperAdmin créé au démarrage.");
                return;
            }

            var existing = await userManager.FindByEmailAsync(superAdminEmail);
            if (existing != null)
            {
                if (existing.BoutiqueId != null || !await userManager.IsInRoleAsync(existing, "SuperAdmin"))
                    logger.LogError("Le compte configuré existe sans rôle SuperAdmin. Aucune promotion automatique n'a été effectuée.");
                return;
            }

            var user = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                EmailConfirmed = true,
                FullName = "Super Administrateur",
                BoutiqueId = null
            };

            var result = await userManager.CreateAsync(user, superAdminPassword);
            if (result.Succeeded)
            {
                if (!(await userManager.AddToRoleAsync(user, "SuperAdmin")).Succeeded)
                    throw new InvalidOperationException("Le compte initial n'a pas reçu le rôle SuperAdmin.");
                logger.LogInformation("Compte SuperAdmin initialisé avec succès.");
            }
            else
            {
                logger.LogError("Création du SuperAdmin impossible : {Errors}",
                    string.Join(" | ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
