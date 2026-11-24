using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginWithEmailModel : PageModel
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly EmailLoginTicket _ticket;
    private readonly AccountAccessGuard _accessGuard;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LoginWithEmailModel> _logger;

    public LoginWithEmailModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        EmailLoginTicket ticket,
        AccountAccessGuard accessGuard,
        IMemoryCache cache,
        ILogger<LoginWithEmailModel> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _ticket = ticket;
        _accessGuard = accessGuard;
        _cache = cache;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ReturnUrl { get; private set; } = string.Empty;

    public class InputModel
    {
        [Required(ErrorMessage = "L'adresse e-mail est obligatoire.")]
        [EmailAddress(ErrorMessage = "Adresse e-mail invalide.")]
        [StringLength(256)]
        [Display(Name = "Adresse e-mail")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Maintenir la session active")]
        public bool RememberMe { get; set; }
    }

    public IActionResult OnGet(string? returnUrl = null, bool expired = false)
    {
        ReturnUrl = AccountNavigation.AfterSignIn(Url, returnUrl);
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(ReturnUrl);
        if (expired)
            ModelState.AddModelError(string.Empty, "Le délai de saisie du code a expiré. Demandez un nouveau code.");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = AccountNavigation.AfterSignIn(Url, returnUrl);
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(ReturnUrl);
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim();
        var cacheKey = $"email-login:{email.ToLowerInvariant()}";
        var recentlySent = _cache.TryGetValue(cacheKey, out _);
        if (!recentlySent)
            _cache.Set(cacheKey, true, Cooldown);

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            // Anti-énumération : même parcours visuel, mais aucun e-mail n'est envoyé.
            _ticket.Set(string.Empty, email, Input.RememberMe);
            return RedirectToPage("./LoginWithEmailCode", new { ReturnUrl, RememberMe = Input.RememberMe });
        }

        if (await _userManager.IsLockedOutAsync(user))
            return RedirectToPage("./Lockout");

        if (!await _accessGuard.CanSignInAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Votre boutique est actuellement désactivée. Veuillez contacter le support AdGestionHub.");
            return Page();
        }

        // La 2FA (TOTP) reste le seul second facteur des comptes qui l'ont activée :
        // un code e-mail ne doit pas permettre de la contourner.
        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            ModelState.AddModelError(string.Empty,
                "Ce compte est protégé par la double authentification. Connectez-vous avec votre mot de passe, puis saisissez votre code.");
            return Page();
        }

        if (!recentlySent)
        {
            try
            {
                var code = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
                await _emailSender.SendEmailAsync(user.Email, "Votre code de connexion AD Gestion Hub",
                    AccountEmails.LoginCode(code));
            }
            catch (Exception error)
            {
                _cache.Remove(cacheKey);
                _logger.LogError(error, "L'envoi du code de connexion par e-mail a échoué pour {Email}.", email);
                ModelState.AddModelError(string.Empty, "L'envoi du code a échoué. Réessayez dans quelques instants.");
                return Page();
            }
        }

        _ticket.Set(user.Id, user.Email, Input.RememberMe);
        return RedirectToPage("./LoginWithEmailCode", new { ReturnUrl, RememberMe = Input.RememberMe });
    }
}
