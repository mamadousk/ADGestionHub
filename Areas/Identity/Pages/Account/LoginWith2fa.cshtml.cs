using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace AdGestionHub.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginWith2faModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<LoginWith2faModel> _logger;

        public LoginWith2faModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<LoginWith2faModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        /// <summary>
        /// Code de secours reçu par e-mail. Doit rester nullable : le formulaire TOTP ne poste pas
        /// ce champ, et une propriété non nullable recevrait un [Required] implicite qui rendrait
        /// ModelState invalide et rejetterait tout code TOTP correct.
        /// </summary>
        [BindProperty]
        public string? EmailCode { get; set; }

        public bool EmailCodeSent { get; set; }

        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; } = string.Empty;

        public class InputModel
        {
            [Required(ErrorMessage = "Le code temporaire est obligatoire.")]
            [StringLength(7, ErrorMessage = "Le code doit comporter entre {2} et {1} caractères.", MinimumLength = 6)]
            [DataType(DataType.Text)]
            [Display(Name = "Code temporaire")]
            public string TwoFactorCode { get; set; } = string.Empty;

            [Display(Name = "Faire confiance à cet appareil")]
            public bool RememberMachine { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(bool rememberMe, string? returnUrl = null)
        {
            ReturnUrl = AccountNavigation.AfterSignIn(Url, returnUrl);
            RememberMe = rememberMe;
            EmailCodeSent = TempData.TryGetValue("EmailCodeSent", out var sent) && sent is true;
            if (User.Identity?.IsAuthenticated == true) return LocalRedirect(ReturnUrl);
            return await _signInManager.GetTwoFactorAuthenticationUserAsync() == null ? RestartLogin() : Page();
        }

        public async Task<IActionResult> OnPostAsync(bool rememberMe, string? returnUrl = null)
        {
            ReturnUrl = AccountNavigation.AfterSignIn(Url, returnUrl);
            RememberMe = rememberMe;
            EmailCodeSent = TempData.TryGetValue("EmailCodeSent", out var sent) && sent is true;
            if (User.Identity?.IsAuthenticated == true) return LocalRedirect(ReturnUrl);

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null) return RestartLogin();
            if (!ModelState.IsValid) return Page();

            var authenticatorCode = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);

            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, rememberMe, Input.RememberMachine);

            if (result.Succeeded)
            {
                _logger.LogInformation("User with ID '{UserId}' logged in with 2fa.", user.Id);
                return LocalRedirect(ReturnUrl);
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User with ID '{UserId}' account locked out.", user.Id);
                return RedirectToPage("./Lockout");
            }
            else
            {
                _logger.LogWarning("Invalid authenticator code entered for user with ID '{UserId}'.", user.Id);
                ModelState.AddModelError(string.Empty, "Le code temporaire est incorrect.");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostSendEmailCodeAsync(bool rememberMe, string? returnUrl = null)
        {
            ReturnUrl = AccountNavigation.AfterSignIn(Url, returnUrl);
            RememberMe = rememberMe;
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null) return RestartLogin();

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                ModelState.AddModelError(string.Empty, "Aucune adresse e-mail n’est associée à ce compte.");
                return Page();
            }

            try
            {
                var code = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
                await _emailSender.SendEmailAsync(user.Email, "Votre code de double authentification AD Gestion Hub",
                    AccountEmails.TwoFactorCode(code));
            }
            catch (Exception error)
            {
                _logger.LogError(error, "L'envoi du code 2FA par e-mail a échoué pour l'utilisateur {UserId}.", user.Id);
                ModelState.AddModelError(string.Empty, "L'envoi du code a échoué. Réessayez dans quelques instants.");
                return Page();
            }

            TempData["EmailCodeSent"] = true;
            return RedirectToPage("./LoginWith2fa", new { rememberMe, returnUrl });
        }

        public async Task<IActionResult> OnPostVerifyEmailCodeAsync(bool rememberMe, string? returnUrl = null)
        {
            ReturnUrl = AccountNavigation.AfterSignIn(Url, returnUrl);
            RememberMe = rememberMe;
            EmailCodeSent = true;

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null) return RestartLogin();

            // Seul le code e-mail est validé ici : le formulaire de code e-mail ne poste pas
            // Input.TwoFactorCode, donc ModelState ne doit pas être contrôlé globalement.
            var code = (EmailCode ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim();
            if (code.Length != 6 || code.Any(c => c is < '0' or > '9'))
            {
                ModelState.AddModelError(nameof(EmailCode), "Le code doit comporter 6 chiffres.");
                return Page();
            }

            var result = await _signInManager.TwoFactorSignInAsync(
                TokenOptions.DefaultEmailProvider, code, rememberMe, Input.RememberMachine);

            if (result.Succeeded)
            {
                _logger.LogInformation("User with ID '{UserId}' logged in with an e-mail 2FA code.", user.Id);
                return LocalRedirect(ReturnUrl);
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User with ID '{UserId}' account locked out after e-mail 2FA attempts.", user.Id);
                return RedirectToPage("./Lockout");
            }

            _logger.LogWarning("Invalid e-mail 2FA code entered for user with ID '{UserId}'.", user.Id);
            ModelState.AddModelError(nameof(EmailCode), "Le code e-mail est incorrect ou a expiré.");
            return Page();
        }

        private IActionResult RestartLogin() => RedirectToPage("./Login", new { ReturnUrl, expired = true });
    }
}
