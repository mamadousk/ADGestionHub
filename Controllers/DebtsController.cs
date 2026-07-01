// ==================== FILE: Controllers/DebtsController.cs ====================
using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

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

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            IQueryable<Debt> query = _context.Debts;

            if (!User.IsInRole("SuperAdmin"))
            {
                query = query.Where(d => d.BoutiqueId == user.BoutiqueId && !d.IsArchived);
            }
            // SuperAdmin voit tout

            var debts = await query.OrderByDescending(d => d.DateCreated).AsNoTracking().ToListAsync();
            return View(debts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Debt debt)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            debt.BoutiqueId = user.BoutiqueId ?? 0;
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
            if (user == null) return Challenge();

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
            if (user == null) return Challenge();

            var debt = await _context.Debts
                .FirstOrDefaultAsync(d => d.Id == id && d.BoutiqueId == user.BoutiqueId);

            if (debt != null)
            {
                debt.IsPaid = true;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Dette marquée comme payée.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}