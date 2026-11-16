using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class DebtsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICacheService _cacheService;

        public DebtsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICacheService cacheService)
        {
            _context = context;
            _userManager = userManager;
            _cacheService = cacheService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            IQueryable<Debt> query = _context.Debts.AsNoTracking();

            if (!User.IsInRole("SuperAdmin"))
            {
                if (user.BoutiqueId == null)
                    return Challenge();

                query = query.Where(d => d.BoutiqueId == user.BoutiqueId.Value && !d.IsArchived);
            }

            var debts = await query
                .OrderByDescending(d => d.DateCreated)
                .ToListAsync();

            return View(debts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DebtCreateViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Veuillez vérifier les informations saisies (nom obligatoire et montant supérieur à 0).";
                return RedirectToAction(nameof(Index));
            }

            var boutiqueId = user.BoutiqueId.Value;
            var debt = new Debt
            {
                ClientName = model.ClientName.Trim(),
                ClientPhone = string.IsNullOrWhiteSpace(model.ClientPhone) ? null : model.ClientPhone.Trim(),
                Amount = decimal.Round(model.Amount, 2),
                DueDate = model.DueDate,
                Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(),
                BoutiqueId = boutiqueId,
                DateCreated = DateTime.Now,
                IsPaid = false,
                IsArchived = false
            };

            _context.Debts.Add(debt);
            await _context.SaveChangesAsync();
            await _cacheService.RemoveAsync($"Dashboard_{boutiqueId}");

            TempData["SuccessMessage"] = "Crédit client enregistré avec succès !";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var debt = await _context.Debts
                .FirstOrDefaultAsync(d => d.Id == id && d.BoutiqueId == boutiqueId);

            if (debt != null)
            {
                debt.IsArchived = true;
                await _context.SaveChangesAsync();
                await _cacheService.RemoveAsync($"Dashboard_{boutiqueId}");
                TempData["SuccessMessage"] = "La dette a été archivée.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var debt = await _context.Debts
                .FirstOrDefaultAsync(d => d.Id == id && d.BoutiqueId == boutiqueId);

            if (debt != null)
            {
                debt.IsPaid = true;
                await _context.SaveChangesAsync();
                await _cacheService.RemoveAsync($"Dashboard_{boutiqueId}");
                TempData["SuccessMessage"] = $"La dette de {debt.ClientName} ({debt.Amount:N2} $) a été marquée comme encaissée.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.BoutiqueId == null)
                return Challenge();

            var boutiqueId = user.BoutiqueId.Value;
            var debt = await _context.Debts
                .FirstOrDefaultAsync(d => d.Id == id && d.BoutiqueId == boutiqueId);

            if (debt != null)
            {
                _context.Debts.Remove(debt);
                await _context.SaveChangesAsync();
                await _cacheService.RemoveAsync($"Dashboard_{boutiqueId}");
                TempData["SuccessMessage"] = "L'enregistrement de dette a été supprimé.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
