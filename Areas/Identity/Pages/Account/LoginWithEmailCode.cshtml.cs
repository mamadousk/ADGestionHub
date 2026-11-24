using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginWithEmailCodeModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly EmailLoginTicket _ticket;
    private readonly AccountAccessGuard _accessGuard;
    private readonly ILogger<LoginWithEmailCodeModel> _logger;

    public LoginWithEmailCodeModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        EmailLoginTicket ticket,
        AccountAccessGuard accessGuard,
        ILogger<LoginWithEmailCodeModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _ticket = ticket;
        _accessGuard = accessGuard;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ReturnUrl { get; private set; } = string.Empty;

    public string MaskedEmail { get; private set; } = string.Empty;

    public class InputModel
    {
        [Required(ErrorMessage = "Le code est obligatoire.")]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "Le code doit comporter 6 chiffres.")]
        [RegularExpression("^[0-9]{6}$", ErrorMessage = "Le code doit comporter 6 chiffres.")]
        [Display(Name = "Code de connexion")]
        public string Code { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        ReturnUrl = AccountNavigation.AfterSignIn(Url, returnUrl);
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(ReturnUrl);

        var ticket = _ticket.Read();
        if (ticket == null)
            return RedirectToPage("./LoginWithEmail", new { ReturnUrl, expired = true });

        MaskedEmail = MaskEmail(ticket.Email);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = AccountNavigation.AfterSignIn(Url, returnUrl);
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(ReturnUrl);

        var ticket = _ticket.Read();
        if (ticket == null)
            return RedirectToPage("./LoginWithEmail", new { ReturnUrl, expired = true });

        MaskedEmail = MaskEmail(ticket.Email);
        if (!ModelState.IsValid) return Page();

        if (string.IsNullOrEmpty(ticket.UserId))
        {
            ModelState.AddModelError(string.Empty, "Le code est incorrect ou a expiré.");
            return Page();
        }

        var user = await _userManager.FindByIdAsync(ticket.UserId);
        if (user == null || !string.Equals(user.Email, ticket.Email, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Le code est incorrect ou a expiré.");
            return Page();
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            _ticket.Clear();
            return RedirectToPage("./Lockout");
        }

        if (!await _accessGuard.CanSignInAsync(user))
        {
            _ticket.Clear();
            ModelState.AddModelError(string.Empty, "Votre boutique est actuellement désactivée. Veuillez contacter le support AdGestionHub.");
            return Page();
        }

        var code = Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty).Trim();
        if (!await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider, code))
        {
            await _userManager.AccessFailedAsync(user);
            if (await _userManager.IsLockedOutAsync(user))
            {
                _ticket.Clear();
                return RedirectToPage("./Lockout");
            }

            _logger.LogWarning("Code de connexion par e-mail invalide pour l'utilisateur {UserId}.", user.Id);
            ModelState.AddModelError(string.Empty, "Le code est incorrect ou a expiré.");
            return Page();
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        // Un compte protégé par la double authentification doit passer par son second facteur :
        // le parcours par code e-mail ne remplace pas un TOTP déjà configuré.
        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            _ticket.Clear();
            _logger.LogWarning("Connexion par code e-mail refusée : la 2FA est active pour l'utilisateur {UserId}.", user.Id);
            ModelState.AddModelError(string.Empty,
                "Ce compte est protégé par la double authentification. Connectez-vous avec votre mot de passe, puis saisissez votre code.");
            return Page();
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            var updated = await _userManager.UpdateAsync(user);
            if (!updated.Succeeded)
                _logger.LogWarning("L'adresse e-mail de l'utilisateur {UserId} n'a pas pu être marquée comme confirmée.", user.Id);
        }

        // Invalide le code et les anciennes sessions avant de créer la nouvelle.
        await _userManager.UpdateSecurityStampAsync(user);
        _ticket.Clear();

        await _signInManager.SignInAsync(user, ticket.RememberMe);

        _logger.LogInformation("Connexion par code e-mail réussie pour l'utilisateur {UserId}.", user.Id);
        return LocalRedirect(ReturnUrl);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return email;

        var local = email[..at];
        var domain = email[at..];
        var visible = local[..Math.Min(2, local.Length)];
        var hidden = new string('*', Math.Max(2, local.Length - visible.Length));
        return $"{visible}{hidden}{domain}";
    }
}
