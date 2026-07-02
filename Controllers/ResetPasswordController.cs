using AdGestionHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

public class ResetPasswordController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public ResetPasswordController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> ResetSuperAdmin()
    {
        const string email = "mamadousacko716@gmail.com";
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return Content("Utilisateur non trouvé.");
        }

        // Générer un token de réinitialisation
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, "Mamadousacko22@");

        if (result.Succeeded)
        {
            // S'assurer que le rôle SuperAdmin est attribué
            if (!await _userManager.IsInRoleAsync(user, "SuperAdmin"))
            {
                await _userManager.AddToRoleAsync(user, "SuperAdmin");
            }
            return Content("Mot de passe réinitialisé avec succès. Utilisez mamadousacko716@gmail.com / Mamadousacko22@ pour vous connecter.");
        }
        else
        {
            return Content("Erreur : " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}