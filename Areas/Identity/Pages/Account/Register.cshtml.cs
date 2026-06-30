using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdGestionHub.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender; // Vérifie bien l'underscore et l'orthographe
        public RegisterModel(
             UserManager<ApplicationUser> userManager,
             IUserStore<ApplicationUser> userStore,
             SignInManager<ApplicationUser> signInManager,
             ILogger<RegisterModel> logger,
             IEmailSender emailSender,
             ApplicationDbContext context,
             RoleManager<IdentityRole> roleManager) // <-- IL MANQUAIT CETTE LIGNE ICI
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = (IUserEmailStore<ApplicationUser>)userStore;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
            _roleManager = roleManager; // <-- CETTE LIGNE SERA MAINTENANT VALIDE
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Le nom de la boutique est requis.")]
            [Display(Name = "Nom de la Boutique")]
            public string StoreName { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "Le {0} doit faire au moins {2} caractères.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mot de passe")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirmer le mot de passe")]
            [Compare("Password", ErrorMessage = "Les mots de passe ne correspondent pas.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // 1. Initialisation de l'utilisateur
                var user = new ApplicationUser
                {
                    FullName = Input.StoreName,
                    StoreName = Input.StoreName
                };

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Compte créé.");

                    // 2. Création de la Boutique (SaaS)
                    var boutique = new Boutique
                    {
                        Name = Input.StoreName,
                        OwnerEmail = user.Email,
                        CreatedAt = DateTime.Now
                    };

                    _context.Boutiques.Add(boutique);
                    await _context.SaveChangesAsync();

                    // 3. Liaison
                    user.BoutiqueId = boutique.Id;
                    await _userManager.UpdateAsync(user);

                    // 4. Attribution du rôle Admin (Sans erreurs)
                    if (!await _roleManager.RoleExistsAsync("Admin"))
                    {
                        await _roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("Admin"));
                    }
                    await _userManager.AddToRoleAsync(user, "Admin");

                    // 5. Connexion
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return Page();
        }
    }
}