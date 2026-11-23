using AdGestionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace AdGestionHub.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ConfirmEmailChangeModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public ConfirmEmailChangeModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [TempData]
    public string StatusMessage { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? userId, string? email, string? code)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
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
            StatusMessage = "Le lien de changement d’adresse est invalide.";
            return Page();
        }

        var result = await _userManager.ChangeEmailAsync(user, email, code);
        if (!result.Succeeded)
        {
            StatusMessage = "Erreur lors du changement d’adresse e-mail. Le lien a peut-être expiré.";
            return Page();
        }

        // Dans notre interface, l'e-mail et le nom d'utilisateur sont le même identifiant.
        var setUserNameResult = await _userManager.SetUserNameAsync(user, email);
        if (!setUserNameResult.Succeeded)
        {
            StatusMessage = "Erreur lors de la mise à jour de l’identifiant.";
            return Page();
        }

        var currentUserId = _userManager.GetUserId(User);
        if (User.Identity?.IsAuthenticated == true && string.Equals(currentUserId, user.Id, StringComparison.Ordinal))
            await _signInManager.RefreshSignInAsync(user);

        StatusMessage = "Merci, votre nouvelle adresse e-mail est confirmée.";
        return Page();
    }
}
