using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

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

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Le nom de votre boutique est obligatoire.")]
            [StringLength(150, ErrorMessage = "Le nom de la boutique ne peut pas dépasser 150 caractères.")]
            [Display(Name = "Nom de la boutique")]
            public string StoreName { get; set; } = string.Empty;

            [Phone(ErrorMessage = "Numéro de téléphone invalide.")]
            [StringLength(40)]
            [Display(Name = "Téléphone")]
            public string? PhoneNumber { get; set; }

            [Required(ErrorMessage = "L'email est obligatoire.")]
            [EmailAddress(ErrorMessage = "Email invalide.")]
            [StringLength(256)]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
            [StringLength(100, ErrorMessage = "Le mot de passe doit comporter au moins {2} caractères.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [Display(Name = "Mot de passe")]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Confirmer le mot de passe")]
            [Compare("Password", ErrorMessage = "Les mots de passe ne correspondent pas.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
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
                ModelState.AddModelError(string.Empty, "Un compte existe déjà avec cet email. Veuillez vous connecter.");
                return Page();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var storeName = string.IsNullOrWhiteSpace(Input.StoreName) ? "Ma Boutique" : Input.StoreName.Trim();
                var phone = Input.PhoneNumber?.Trim() ?? string.Empty;

                var boutique = new Boutique
                {
                    Nom = storeName,
                    Email = email,
                    Telephone = phone,
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
                    Phone = phone,
                    ContactEmail = email,
                    FooterMessage = "Merci de votre achat et à très bientôt !",
                    IsActive = true
                };
                _context.StoreSettings.Add(settings);
                await _context.SaveChangesAsync();

                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    PhoneNumber = string.IsNullOrEmpty(phone) ? null : phone,
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
                            message = "Cet email est déjà utilisé par un compte existant.";
                        else if (message.Contains("digit", StringComparison.OrdinalIgnoreCase))
                            message = "Le mot de passe doit contenir au moins un chiffre.";
                        else if (message.Contains("uppercase", StringComparison.OrdinalIgnoreCase))
                            message = "Le mot de passe doit contenir au moins une lettre majuscule.";
                        else if (message.Contains("lowercase", StringComparison.OrdinalIgnoreCase))
                            message = "Le mot de passe doit contenir au moins une lettre minuscule.";
                        else if (message.Contains("non-alphanumeric", StringComparison.OrdinalIgnoreCase))
                            message = "Le mot de passe doit contenir au moins un caractère spécial (ex: @, !, #).";

                        ModelState.AddModelError(string.Empty, message);
                    }
                    return Page();
                }

                await _userManager.AddToRoleAsync(user, "Admin");
                await transaction.CommitAsync();

                _logger.LogInformation("Nouvelle boutique #{BoutiqueId} et compte Admin créés.", boutique.Id);

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
                        await _emailSender.SendEmailAsync(email, "Bienvenue sur AdGestionHub",
                            $"Confirmez votre adresse email en <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>cliquant ici</a>.");
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
                ModelState.AddModelError(string.Empty, "Une erreur technique est survenue lors de l'inscription. Veuillez réessayer.");
                return Page();
            }
        }
    }
}
