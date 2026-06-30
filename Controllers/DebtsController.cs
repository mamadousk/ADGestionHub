using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class DebtsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DebtsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Liste des dettes actives (Non archivées)
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var query = _context.Debts.AsQueryable();

            // LOGIQUE DE POUVOIR DU SUPERADMIN
            if (!User.IsInRole("SuperAdmin"))
            {
                // Si ce n'est pas toi, on filtre par boutique
                query = query.Where(d => d.BoutiqueId == user.BoutiqueId && !d.IsArchived);
            }
            // Si c'est toi (SuperAdmin), la requête reste "brute" et prend TOUT

            var debts = await query.OrderByDescending(d => d.DateCreated).ToListAsync();
            return View(debts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Debt debt)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // CORRECTION ERREUR CS0266 : On utilise GetValueOrDefault() pour passer de int? à int
            debt.BoutiqueId = user.BoutiqueId.GetValueOrDefault();
            debt.DateCreated = DateTime.Now;
            debt.IsArchived = false;

            _context.Add(debt);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Dette enregistrée avec succès !";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Archive(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            // On vérifie que la dette appartient bien à la boutique de l'utilisateur
            var debt = await _context.Debts
                .FirstOrDefaultAsync(d => d.Id == id && d.BoutiqueId == user.BoutiqueId);

            if (debt != null)
            {
                debt.IsArchived = true;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "La dette a été déplacée vers les archives.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var debt = await _context.Debts
                .FirstOrDefaultAsync(d => d.Id == id && d.BoutiqueId == user.BoutiqueId);

            if (debt != null)
            {
                debt.IsPaid = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}