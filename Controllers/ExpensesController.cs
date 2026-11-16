using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers
{
    [Authorize]
    public class ExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBoutiqueService _boutiqueService;
        private readonly ITenantContextService _tenantContext;
        private readonly ICacheService _cacheService;
        private readonly IAppLocalizer _L;

        public ExpensesController(
            ApplicationDbContext context,
            IBoutiqueService boutiqueService,
            ITenantContextService tenantContext,
            ICacheService cacheService,
            IAppLocalizer localizer)
        {
            _context = context;
            _boutiqueService = boutiqueService;
            _tenantContext = tenantContext;
            _cacheService = cacheService;
            _L = localizer;
        }

        public async Task<IActionResult> Index()
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null)
                return Challenge();

            var expenses = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.BoutiqueId == boutiqueId.Value)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            return View(expenses);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null)
                return Challenge();

            var expense = await _context.Expenses
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id.Value && e.BoutiqueId == boutiqueId.Value);

            if (expense == null) return NotFound();
            return View(expense);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var ctx = await _tenantContext.GetContextAsync();
            return View(new Expense
            {
                Date = DateTime.Now,
                Category = "Loyer",
                CurrencyCode = ctx.PrimaryCurrency.Code,
                CurrencySymbol = ctx.PrimaryCurrency.Symbol,
                CurrencyDecimals = ctx.PrimaryCurrency.Decimals
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExpenseFormViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (string.IsNullOrEmpty(userId) || boutiqueId == null)
                return Challenge();

            var ctx = await _tenantContext.GetContextAsync();

            if (!ModelState.IsValid)
            {
                return View(new Expense
                {
                    Description = model.Description,
                    Amount = model.Amount,
                    Date = model.Date,
                    Category = model.Category,
                    CurrencyCode = ctx.PrimaryCurrency.Code,
                    CurrencySymbol = ctx.PrimaryCurrency.Symbol,
                    CurrencyDecimals = ctx.PrimaryCurrency.Decimals
                });
            }

            var expense = new Expense
            {
                Description = model.Description.Trim(),
                Amount = decimal.Round(model.Amount, ctx.PrimaryCurrency.Decimals),
                CurrencyCode = ctx.PrimaryCurrency.Code,
                CurrencySymbol = ctx.PrimaryCurrency.Symbol,
                CurrencyDecimals = ctx.PrimaryCurrency.Decimals,
                Date = model.Date == default ? DateTime.Now : model.Date,
                Category = string.IsNullOrWhiteSpace(model.Category) ? "Autre" : model.Category.Trim(),
                BoutiqueId = boutiqueId.Value,
                UserId = userId
            };

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();
            await _cacheService.RemoveAsync($"Dashboard_{boutiqueId.Value}");

            TempData["SuccessMessage"] = _L.T("Dépense enregistrée.", "Expense recorded.");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null)
                return Challenge();

            var expense = await _context.Expenses
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id.Value && e.BoutiqueId == boutiqueId.Value);

            if (expense == null) return NotFound();
            return View(expense);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, ExpenseFormViewModel model)
        {
            if (id != model.Id) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null)
                return Challenge();

            var existing = await _context.Expenses
                .FirstOrDefaultAsync(e => e.Id == id && e.BoutiqueId == boutiqueId.Value);

            if (existing == null) return NotFound();

            if (!ModelState.IsValid)
            {
                return View(new Expense
                {
                    Id = id,
                    Description = model.Description,
                    Amount = model.Amount,
                    Date = model.Date,
                    Category = model.Category,
                    CurrencyCode = existing.CurrencyCode,
                    CurrencySymbol = existing.CurrencySymbol,
                    CurrencyDecimals = existing.CurrencyDecimals
                });
            }

            // Conserver la devise d'origine de la dépense historique
            existing.Description = model.Description.Trim();
            existing.Amount = decimal.Round(model.Amount, existing.CurrencyDecimals);
            existing.Date = model.Date == default ? existing.Date : model.Date;
            existing.Category = string.IsNullOrWhiteSpace(model.Category) ? "Autre" : model.Category.Trim();

            await _context.SaveChangesAsync();
            await _cacheService.RemoveAsync($"Dashboard_{boutiqueId.Value}");

            TempData["SuccessMessage"] = _L.T("Dépense mise à jour.", "Expense updated.");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null)
                return Challenge();

            var expense = await _context.Expenses
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id.Value && e.BoutiqueId == boutiqueId.Value);

            if (expense == null) return NotFound();
            return View(expense);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var boutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (boutiqueId == null)
                return Challenge();

            var expense = await _context.Expenses
                .FirstOrDefaultAsync(e => e.Id == id && e.BoutiqueId == boutiqueId.Value);

            if (expense != null)
            {
                _context.Expenses.Remove(expense);
                await _context.SaveChangesAsync();
                await _cacheService.RemoveAsync($"Dashboard_{boutiqueId.Value}");
                TempData["SuccessMessage"] = _L.T("Dépense supprimée.", "Expense deleted.");
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
