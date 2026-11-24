using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace AdGestionHub.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string ReturnUrl { get; private set; } = string.Empty;

        public class InputModel
        {
            [Required(ErrorMessage = "L'email ou le nom d'utilisateur est obligatoire.")]
            [StringLength(256)]
            [Display(Name = "Email ou nom d'utilisateur")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
            [DataType(DataType.Password)]
            [Display(Name = "Mot de passe")]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Se souvenir de moi")]
            public bool RememberMe { get; set; }
        }

        public IActionResult OnGet(string? returnUrl = null, int? suspended = null, bool expired = false)
        {
            ReturnUrl = AccountNavigation.AfterSignIn(Url, returnUrl);
            if (User.Identity?.IsAuthenticated == true) return LocalRedirect(ReturnUrl);

            if (suspended == 1)
            {
                ModelState.AddModelError(string.Empty, "L'accès à votre boutique est temporairement suspendu. Veuillez contacter l'administrateur de la plateforme.");
            }
            else if (expired)
            {
                ModelState.AddModelError(string.Empty, "La vérification de connexion a expiré. Reconnectez-vous.");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = AccountNavigation.AfterSignIn(Url, returnUrl);
            if (User.Identity?.IsAuthenticated == true) return LocalRedirect(ReturnUrl);

            if (!ModelState.IsValid)
                return Page();

            var identifier = Input.Email.Trim();
            var user = identifier.Contains('@')
                ? await _userManager.FindByEmailAsync(identifier) ?? await _userManager.FindByNameAsync(identifier)
                : await _userManager.FindByNameAsync(identifier) ?? await _userManager.FindByEmailAsync(identifier);

            if (user == null || string.IsNullOrEmpty(user.UserName))
            {
                ModelState.AddModelError(string.Empty, "Identifiant ou mot de passe incorrect.");
                return Page();
            }

            // Vérifier que la boutique associée est bien active avant de créer une session
            if (user.BoutiqueId.HasValue)
            {
                var boutique = await _context.Boutiques
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == user.BoutiqueId.Value);

                if (boutique != null && !boutique.IsActive)
                {
                    _logger.LogWarning("Tentative de connexion refusée : boutique {BoutiqueId} suspendue.", boutique.Id);
                    ModelState.AddModelError(string.Empty, "Votre boutique est actuellement désactivée. Veuillez contacter le support AdGestionHub.");
                    return Page();
                }
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("Connexion réussie pour un utilisateur.");
                return LocalRedirect(ReturnUrl);
            }
            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl, RememberMe = Input.RememberMe });
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning("Compte temporairement verrouillé suite à plusieurs tentatives échouées.");
                return RedirectToPage("./Lockout");
            }

            ModelState.AddModelError(string.Empty, "Identifiant ou mot de passe incorrect.");
            return Page();
        }
    }
}
