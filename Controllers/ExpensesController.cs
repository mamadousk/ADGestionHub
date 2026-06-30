using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdGestionHub.Data;
using AdGestionHub.Models;
using System.Security.Claims;

namespace AdGestionHub.Controllers
{
    public class ExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExpensesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var expenses = await _context.Expenses
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            return View(expenses);
        }

        // --- CETTE MÉTHODE MANQUAIT ET CAUSAIT LA 404 ---
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Expense expense)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId != null)
            {
                // On récupère l'utilisateur pour lier la dépense à sa boutique
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                expense.UserId = userId;
                if (user != null) expense.BoutiqueId = user.BoutiqueId;

                if (ModelState.IsValid)
                {
                    _context.Add(expense);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(expense);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense != null)
            {
                _context.Expenses.Remove(expense);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}