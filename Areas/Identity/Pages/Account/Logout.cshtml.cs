using System.Threading.Tasks;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace AdGestionHub.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(SignInManager<ApplicationUser> signInManager, ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        // Sécurité : jamais de déconnexion via GET (prévention Logout CSRF).
        public IActionResult OnGet() => User.Identity?.IsAuthenticated == true
            ? RedirectToAction("Index", "Home", new { area = "" })
            : RedirectToPage("./Login");

        public async Task<IActionResult> OnPostAsync()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("L'utilisateur s'est déconnecté.");
            return RedirectToPage("./Login");
        }
    }
}
