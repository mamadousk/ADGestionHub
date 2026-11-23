using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AdGestionHub.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender,
        ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "L'adresse e-mail est obligatoire.")]
        [EmailAddress(ErrorMessage = "Adresse e-mail invalide.")]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
            return RedirectToPage("./ForgotPasswordConfirmation");

        try
        {
            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                // Une adresse non confirmée ne reçoit pas de lien de réinitialisation.
                // On lui renvoie plutôt le lien de confirmation, sans révéler l'existence du compte.
                var confirmation = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                confirmation = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(confirmation));
                var confirmationUrl = Url.Page("/Account/ConfirmEmail", pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code = confirmation }, protocol: Request.Scheme);
                await _emailSender.SendEmailAsync(user.Email, "Confirmez votre adresse e-mail",
                    AccountEmails.ConfirmEmail(confirmationUrl));
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page("/Account/ResetPassword", pageHandler: null,
                values: new { area = "Identity", code, email = user.Email }, protocol: Request.Scheme);
            await _emailSender.SendEmailAsync(user.Email, "Réinitialisation de votre mot de passe",
                AccountEmails.ResetPassword(callbackUrl));
        }
        catch (Exception error)
        {
            _logger.LogError(error, "L'envoi de l'e-mail de réinitialisation a échoué pour {Email}.", email);
        }

        return RedirectToPage("./ForgotPasswordConfirmation");
    }
}
