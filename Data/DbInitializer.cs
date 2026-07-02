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

            // 1. Création des rôles
            string[] roleNames = { "Admin", "Employé", "SuperAdmin" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Création du SuperAdmin, uniquement si ses identifiants sont fournis par la configuration
            //    (variables d'environnement SuperAdmin__Email / SuperAdmin__Password, ou User Secrets en développement).
            var superAdminEmail = configuration["mamadousacko716@gmail.com"];
            var superAdminPassword = configuration["Mamadousacko22@"];

            if (string.IsNullOrWhiteSpace(superAdminEmail) || string.IsNullOrWhiteSpace(superAdminPassword))
            {
                logger.LogWarning(
                    "SuperAdmin:Email ou SuperAdmin:Password n'est pas configuré : aucun compte SuperAdmin n'a été créé.");
                return;
            }

            var existing = await userManager.FindByEmailAsync(superAdminEmail);
            if (existing != null)
            {
                // Le compte existe déjà : on ne touche jamais à son mot de passe.
                return;
            }

            var user = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                EmailConfirmed = true,
                FullName = "Super Administrateur",
                StoreName = "AdGestionHub Global",
                BoutiqueId = null // Le SuperAdmin n'a pas de boutique attitrée
            };

            var result = await userManager.CreateAsync(user, superAdminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "SuperAdmin");
                logger.LogInformation("Compte SuperAdmin créé.");
            }
            else
            {
                // On journalise les raisons (mot de passe trop faible, etc.) sans jamais écrire le mot de passe.
                logger.LogError("Création du SuperAdmin impossible : {Errors}",
                    string.Join(" | ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}

