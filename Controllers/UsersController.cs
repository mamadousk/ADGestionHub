using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdGestionHub.Data;
using AdGestionHub.Models;
using System.Linq;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentAdmin = await _userManager.GetUserAsync(User);

            // On ne liste que les employés de sa propre boutique
            var users = await _userManager.Users
                .Where(u => u.BoutiqueId == currentAdmin.BoutiqueId)
                .ToListAsync();

            return View(users);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "L'email et le mot de passe sont requis.");
                return View();
            }

            var currentAdmin = await _userManager.GetUserAsync(User);

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                BoutiqueId = currentAdmin.BoutiqueId,
                FullName = "Employé " + currentAdmin.StoreName // Valeur par défaut
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Employé");
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View();
        }
    }
}