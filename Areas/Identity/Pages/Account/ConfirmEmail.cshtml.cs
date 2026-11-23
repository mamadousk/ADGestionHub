using AdGestionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace AdGestionHub.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ConfirmEmailModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public ConfirmEmailModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [TempData]
    public string StatusMessage { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? userId, string? code)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
            return RedirectToPage("./Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("Impossible de charger l’utilisateur.");

        try
        {
            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            StatusMessage = "Le lien de confirmation est invalide ou a été modifié.";
            return Page();
        }

        var result = await _userManager.ConfirmEmailAsync(user, code);
        if (result.Succeeded)
        {
            StatusMessage = "Merci, votre adresse e-mail est confirmée.";
            var currentUserId = _userManager.GetUserId(User);
            if (User.Identity?.IsAuthenticated == true && string.Equals(currentUserId, user.Id, StringComparison.Ordinal))
                await _signInManager.RefreshSignInAsync(user);
        }
        else
        {
            StatusMessage = "Erreur lors de la confirmation. Le lien a peut-être expiré ou a déjà été utilisé.";
        }

        return Page();
    }
}
