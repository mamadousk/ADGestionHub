using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SettingsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var settings = await _context.StoreSettings
                .FirstOrDefaultAsync(s => s.BoutiqueId == user.BoutiqueId);

            if (settings == null)
            {
                var boutique = await _context.Boutiques.FindAsync(user.BoutiqueId);
                settings = new StoreSettings
                {
                    StoreName = boutique?.Name ?? "Mon Enseigne",
                    BoutiqueId = user.BoutiqueId ?? 0
                };
            }

            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(StoreSettings model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var settingsInDb = await _context.StoreSettings
                .FirstOrDefaultAsync(s => s.BoutiqueId == user.BoutiqueId);

            if (settingsInDb != null)
            {
                // Mise à jour de l'existant
                settingsInDb.StoreName = model.StoreName;
                settingsInDb.Address = model.Address;
                settingsInDb.PhoneNumber = model.PhoneNumber;
                settingsInDb.Email = model.Email;
                settingsInDb.ReceiptFooterMessage = model.ReceiptFooterMessage;

                _context.Update(settingsInDb);
            }
            else
            {
                // Premier enregistrement : On force l'ID de la boutique de l'utilisateur actuel
                model.BoutiqueId = user.BoutiqueId ?? 0;
                _context.StoreSettings.Add(model);
            }

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Enregistré avec succès !";
                // On redirige vers l'Index pour rafraîchir proprement les données de la base
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Erreur de sauvegarde : " + ex.Message);
                return View(model);
            }
        }
    }
}