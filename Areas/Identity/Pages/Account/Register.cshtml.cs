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
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "L'email est obligatoire.")]
            [EmailAddress(ErrorMessage = "Email invalide.")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
            [StringLength(100, ErrorMessage = "Le mot de passe doit comporter au moins {2} caractères.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mot de passe")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirmer le mot de passe")]
            [Compare("Password", ErrorMessage = "Les mots de passe ne correspondent pas.")]
            public string ConfirmPassword { get; set; }

            [Display(Name = "Nom de la boutique")]
            public string StoreName { get; set; }

            [Display(Name = "Téléphone")]
            public string PhoneNumber { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
                return Page();

            try
            {
                // Vérifier si l'email est déjà utilisé (peu importe le rôle)
                var existingUser = await _userManager.FindByEmailAsync(Input.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError(string.Empty, "Un compte existe déjà avec cet email. Veuillez vous connecter.");
                    return Page();
                }

                // Créer l'utilisateur
                var user = new ApplicationUser
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    EmailConfirmed = true
                };

                // Créer la boutique associée
                var boutique = new Boutique
                {
                    Nom = Input.StoreName ?? "Ma boutique",
                    Email = Input.Email,
                    Telephone = Input.PhoneNumber ?? "",
                    Adresse = "",
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Boutiques.Add(boutique);
                await _context.SaveChangesAsync();

                user.BoutiqueId = boutique.Id;

                // Créer l'utilisateur avec mot de passe
                var result = await _userManager.CreateAsync(user, Input.Password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Nouveau compte créé.");

                    // Attribuer le rôle Admin (le SuperAdmin est créé via seed)
                    await _userManager.AddToRoleAsync(user, "Admin");

                    // Envoyer l'email de confirmation (optionnel)
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = user.Id, code = code },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirmez votre email",
                        $"Veuillez confirmer votre compte en <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>cliquant ici</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email });
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }

                // En cas d'erreur, afficher les messages
                foreach (var error in result.Errors)
                {
                    string message = error.Description;
                    if (message.Contains("Duplicate user name") || message.Contains("already taken"))
                        message = "Cet email est déjà utilisé par un compte existant. Veuillez vous connecter.";
                    else if (message.Contains("Password") && message.Contains("digit"))
                        message = "Le mot de passe doit contenir au moins un chiffre.";
                    else if (message.Contains("Password") && message.Contains("uppercase"))
                        message = "Le mot de passe doit contenir au moins une majuscule.";
                    else if (message.Contains("Password") && message.Contains("lowercase"))
                        message = "Le mot de passe doit contenir au moins une minuscule.";
                    else if (message.Contains("Password") && message.Contains("non-alphanumeric"))
                        message = "Le mot de passe doit contenir au moins un caractère spécial.";
                    ModelState.AddModelError(string.Empty, message);
                }
                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Erreur technique : " + ex.Message);
                return Page();
            }
        }
    }
}