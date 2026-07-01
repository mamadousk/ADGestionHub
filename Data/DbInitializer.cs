// ==================== FILE: Data/DbInitializer.cs ====================
using AdGestionHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace AdGestionHub.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. Création des rôles
            string[] roleNames = { "Admin", "Employé", "SuperAdmin" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Création du SuperAdmin par défaut
            var superAdminEmail = "mamadousacko716@gmail.com";
            var superAdminUser = await userManager.FindByEmailAsync(superAdminEmail);

            if (superAdminUser == null)
            {
                // Création de l'utilisateur
                var user = new ApplicationUser
                {
                    UserName = superAdminEmail,
                    Email = superAdminEmail,
                    EmailConfirmed = true,
                    FullName = "Super Administrateur",
                    StoreName = "AdGestionHub Global",
                    BoutiqueId = null // Le SuperAdmin n'a pas de boutique attitrée, il voit tout
                };

                var result = await userManager.CreateAsync(user, "Mamadousacko22@"); // Mot de passe par défaut (à changer à la première connexion)

                if (result.Succeeded)
                {
                    // Attribution du rôle SuperAdmin
                    await userManager.AddToRoleAsync(user, "SuperAdmin");

                    // Optionnel : créer une boutique "démo" si vous voulez, mais le SuperAdmin n'en a pas besoin.
                    // On peut juste laisser BoutiqueId = null.
                }
            }
        }
    }
}