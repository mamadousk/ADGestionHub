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
public class ResendEmailConfirmationModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ResendEmailConfirmationModel> _logger;

    public ResendEmailConfirmationModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender,
        ILogger<ResendEmailConfirmationModel> logger)
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
        if (user != null && !string.IsNullOrWhiteSpace(user.Email))
        {
            try
            {
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page("/Account/ConfirmEmail", pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code }, protocol: Request.Scheme);
                await _emailSender.SendEmailAsync(user.Email, "Confirmez votre adresse e-mail",
                    AccountEmails.ConfirmEmail(callbackUrl));
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Le renvoi de l'e-mail de confirmation a échoué pour {Email}.", email);
            }
        }

        return RedirectToPage("./RegisterConfirmation");
    }
}
