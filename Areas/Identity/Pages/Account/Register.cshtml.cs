using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace AdGestionHub.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly IAppLocalizer _L;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext context,
            IAppLocalizer localizer)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
            _L = localizer;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Le nom de votre boutique est obligatoire.")]
            [StringLength(150, ErrorMessage = "Le nom de la boutique ne peut pas dépasser 150 caractères.")]
            public string StoreName { get; set; } = string.Empty;

            [Required]
            [StringLength(10)]
            public string CountryCode { get; set; } = "CD";

            [Required]
            [StringLength(10)]
            public string PreferredLanguage { get; set; } = "fr";

            [Phone(ErrorMessage = "Numéro de téléphone invalide.")]
            [StringLength(40)]
            public string? PhoneNumber { get; set; }

            [Required(ErrorMessage = "L'email est obligatoire.")]
            [EmailAddress(ErrorMessage = "Email invalide.")]
            [StringLength(256)]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
            [StringLength(100, ErrorMessage = "Le mot de passe doit comporter au moins {2} caractères.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Les mots de passe ne correspondent pas.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            Input.PreferredLanguage = _L.CurrentLanguage;
            Input.PhoneNumber = "+243 ";
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
                return Page();

            var email = Input.Email.Trim();
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, _L.T(
                    "Un compte existe déjà avec cet email. Veuillez vous connecter.",
                    "An account already exists with this email. Please sign in."));
                return Page();
            }

            var country = CountryProfileCatalog.GetCountry(Input.CountryCode);
            var normalizedLang = string.Equals(Input.PreferredLanguage, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fr";

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var storeName = string.IsNullOrWhiteSpace(Input.StoreName) ? "Ma Boutique" : Input.StoreName.Trim();
                var rawPhone = Input.PhoneNumber?.Trim() ?? string.Empty;
                if (string.Equals(rawPhone, country.PhoneDialCode, StringComparison.OrdinalIgnoreCase))
                    rawPhone = string.Empty;

                var boutique = new Boutique
                {
                    Nom = storeName,
                    Email = email,
                    Telephone = rawPhone,
                    Adresse = string.Empty,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Boutiques.Add(boutique);
                await _context.SaveChangesAsync();

                var settings = new StoreSettings
                {
                    BoutiqueId = boutique.Id,
                    StoreName = storeName,
                    Phone = rawPhone,
                    ContactEmail = email,
                    CountryCode = country.Code,
                    PreferredLanguage = normalizedLang,
                    PrimaryCurrencyCode = country.PrimaryCurrencyCode,
                    SecondaryCurrencyEnabled = false,
                    SecondaryCurrencyCode = country.SuggestedSecondaryCurrencyCode,
                    SecondaryExchangeRate = country.Code == "CD" ? 2850m : (country.PrimaryCurrencyCode == "XOF" || country.PrimaryCurrencyCode == "XAF" ? 655.957m : 1m),
                    PhoneDialCode = country.PhoneDialCode,
                    DateFormat = country.DateFormat,
                    IsFormalized = false,
                    IsVatEnabled = false,
                    DefaultVatRate = country.StandardVatRate,
                    FooterMessage = normalizedLang == "en"
                        ? "Thank you for your business."
                        : "Merci de votre confiance.",
                    IsActive = true
                };
                _context.StoreSettings.Add(settings);
                await _context.SaveChangesAsync();

                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    PhoneNumber = string.IsNullOrEmpty(rawPhone) ? null : rawPhone,
                    EmailConfirmed = true,
                    BoutiqueId = boutique.Id
                };

                var result = await _userManager.CreateAsync(user, Input.Password);
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();

                    foreach (var error in result.Errors)
                    {
                        string message = error.Description;
                        if (message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) || message.Contains("already taken", StringComparison.OrdinalIgnoreCase))
                            message = _L.T("Cet email est déjà utilisé.", "This email is already taken.");
                        else if (message.Contains("digit", StringComparison.OrdinalIgnoreCase))
                            message = _L.T("Le mot de passe doit contenir au moins un chiffre.", "Password must contain at least one digit.");
                        else if (message.Contains("uppercase", StringComparison.OrdinalIgnoreCase))
                            message = _L.T("Le mot de passe doit contenir au moins une majuscule.", "Password must contain at least one uppercase letter.");
                        else if (message.Contains("lowercase", StringComparison.OrdinalIgnoreCase))
                            message = _L.T("Le mot de passe doit contenir au moins une minuscule.", "Password must contain at least one lowercase letter.");
                        else if (message.Contains("non-alphanumeric", StringComparison.OrdinalIgnoreCase))
                            message = _L.T("Le mot de passe doit contenir au moins un caractère spécial.", "Password must contain at least one special character.");

                        ModelState.AddModelError(string.Empty, message);
                    }
                    return Page();
                }

                await _userManager.AddToRoleAsync(user, "Admin");
                await transaction.CommitAsync();

                Response.Cookies.Append(
                    AppLocalizer.LanguageCookieName,
                    normalizedLang,
                    new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        HttpOnly = false,
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax,
                        Secure = Request.IsHttps
                    });

                _logger.LogInformation("Nouvelle boutique #{BoutiqueId} ({Country}/{Currency}) créée.", boutique.Id, country.Code, country.PrimaryCurrencyCode);

                try
                {
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = user.Id, code = code },
                        protocol: Request.Scheme);

                    if (!string.IsNullOrEmpty(callbackUrl))
                    {
                        await _emailSender.SendEmailAsync(email, "AdGestionHub",
                            $"<a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>Confirm</a>");
                    }
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "Échec non bloquant de l'envoi d'email de bienvenue.");
                }

                if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    return RedirectToPage("RegisterConfirmation", new { email });

                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Erreur lors de la création de compte / boutique.");
                ModelState.AddModelError(string.Empty, _L.T(
                    "Une erreur technique est survenue lors de l'inscription.",
                    "A technical error occurred during registration."));
                return Page();
            }
        }
    }
}
