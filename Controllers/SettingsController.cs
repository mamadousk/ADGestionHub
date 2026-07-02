using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize(Roles = "Admin, SuperAdmin")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBoutiqueService _boutiqueService;

        public SettingsController(ApplicationDbContext context, IBoutiqueService boutiqueService)
        {
            _context = context;
            _boutiqueService = boutiqueService;
        }

        // GET: Settings
        public async Task<IActionResult> Index()
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Unauthorized();

            // Récupérer la boutique
            var boutique = await _context.Boutiques.FindAsync(boutiqueId.Value);
            if (boutique == null) return NotFound();

            // Récupérer ou créer StoreSettings
            var settings = await _context.StoreSettings
                .FirstOrDefaultAsync(s => s.BoutiqueId == boutiqueId);

            if (settings == null)
            {
                settings = new StoreSettings
                {
                    BoutiqueId = boutiqueId.Value,
                    StoreName = boutique.Nom,
                    Address = boutique.Adresse,
                    Phone = boutique.Telephone,
                    ContactEmail = boutique.Email,
                    IsActive = true
                };
                _context.StoreSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            // Passer le nom de la boutique au ViewBag pour affichage en lecture seule
            ViewBag.BoutiqueNom = boutique.Nom;

            return View(settings);
        }

        // POST: Settings/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(StoreSettings settings)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Unauthorized();

            if (settings.BoutiqueId != boutiqueId) return Forbid();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(settings);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Paramètres mis à jour avec succès.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StoreSettingsExists(settings.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(settings);
        }

        private bool StoreSettingsExists(int id)
        {
            return _context.StoreSettings.Any(e => e.Id == id);
        }
    }
}