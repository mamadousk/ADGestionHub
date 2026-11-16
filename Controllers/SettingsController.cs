using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
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
            if (boutiqueId == null)
            {
                if (User.IsInRole("SuperAdmin"))
                    return RedirectToAction("Index", "SuperAdmin");
                return Challenge();
            }

            var boutique = await _context.Boutiques.FindAsync(boutiqueId.Value);
            if (boutique == null) return NotFound();

            var settings = await _context.StoreSettings
                .FirstOrDefaultAsync(s => s.BoutiqueId == boutiqueId.Value);

            if (settings == null)
            {
                settings = new StoreSettings
                {
                    BoutiqueId = boutiqueId.Value,
                    StoreName = boutique.Nom,
                    Address = boutique.Adresse,
                    Phone = boutique.Telephone,
                    ContactEmail = boutique.Email,
                    FooterMessage = "Merci de votre achat et à très bientôt !",
                    IsActive = boutique.IsActive
                };
                _context.StoreSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            ViewBag.BoutiqueNom = boutique.Nom;
            return View(settings);
        }

        // POST: Settings/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("Id,BoutiqueId,StoreName,Address,Phone,ContactEmail,Description,FooterMessage")] StoreSettings input)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null) return Challenge();

            if (input.BoutiqueId != boutiqueId.Value)
                return Forbid();

            var boutique = await _context.Boutiques.FindAsync(boutiqueId.Value);
            if (boutique == null) return NotFound();

            var existing = await _context.StoreSettings
                .FirstOrDefaultAsync(s => s.BoutiqueId == boutiqueId.Value);

            if (existing == null) return NotFound();

            // Retirer la validation éventuelle sur la navigation Boutique
            ModelState.Remove(nameof(StoreSettings.Boutique));

            if (!ModelState.IsValid)
            {
                ViewBag.BoutiqueNom = boutique.Nom;
                return View("Index", input);
            }

            var cleanStoreName = string.IsNullOrWhiteSpace(input.StoreName) ? boutique.Nom : input.StoreName.Trim();

            existing.StoreName = cleanStoreName;
            existing.Address = input.Address?.Trim();
            existing.Phone = input.Phone?.Trim();
            existing.ContactEmail = input.ContactEmail?.Trim();
            existing.Description = input.Description?.Trim();
            existing.FooterMessage = input.FooterMessage?.Trim();

            // Synchroniser les coordonnées principales sur l'entité Boutique
            boutique.Nom = cleanStoreName;
            boutique.Adresse = existing.Address ?? string.Empty;
            boutique.Telephone = existing.Phone ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(existing.ContactEmail))
                boutique.Email = existing.ContactEmail;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Les paramètres de votre boutique ont été enregistrés avec succès.";
            return RedirectToAction(nameof(Index));
        }
    }
}
