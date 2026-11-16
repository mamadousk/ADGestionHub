using System;
using System.Linq;
using System.Threading.Tasks;
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class DebtsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantContextService _tenantContext;
        private readonly ICacheService _cacheService;
        private readonly IAppLocalizer _L;

        public DebtsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITenantContextService tenantContext,
            ICacheService cacheService,
            IAppLocalizer localizer)
        {
            _context = context;
            _userManager = userManager;
            _tenantContext = tenantContext;
            _cacheService = cacheService;
            _L = localizer;
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
                TempData["ErrorMessage"] = _L.T(
                    "Veuillez vérifier les informations saisies (nom obligatoire et montant supérieur à 0).",
                    "Please check your input (customer name required and amount greater than 0).");
                return RedirectToAction(nameof(Index));
            }

            var ctx = await _tenantContext.GetContextAsync();
            var boutiqueId = user.BoutiqueId.Value;
            var cleanPhone = model.ClientPhone?.Trim();
            if (string.Equals(cleanPhone, ctx.Settings.PhoneDialCode, StringComparison.OrdinalIgnoreCase))
                cleanPhone = null;

            var debt = new Debt
            {
                ClientId = model.ClientId,
                ClientName = model.ClientName.Trim(),
                ClientPhone = string.IsNullOrWhiteSpace(cleanPhone) ? null : cleanPhone,
                ClientTaxId = ctx.IsFormalized && !string.IsNullOrWhiteSpace(model.ClientTaxId) ? model.ClientTaxId.Trim() : null,
                Amount = decimal.Round(model.Amount, ctx.PrimaryCurrency.Decimals),
                CurrencyCode = ctx.PrimaryCurrency.Code,
                CurrencySymbol = ctx.PrimaryCurrency.Symbol,
                CurrencyDecimals = ctx.PrimaryCurrency.Decimals,
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

            TempData["SuccessMessage"] = _L.T("Créance client enregistrée.", "Customer receivable recorded.");
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
                TempData["SuccessMessage"] = _L.T("Créance archivée.", "Receivable archived.");
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

            var ctx = await _tenantContext.GetContextAsync();
            var boutiqueId = user.BoutiqueId.Value;
            var debt = await _context.Debts
                .FirstOrDefaultAsync(d => d.Id == id && d.BoutiqueId == boutiqueId);

            if (debt != null)
            {
                debt.IsPaid = true;
                await _context.SaveChangesAsync();
                await _cacheService.RemoveAsync($"Dashboard_{boutiqueId}");
                var formatted = ctx.FormatHistorical(debt.Amount, debt.CurrencyCode, debt.CurrencyDecimals, debt.CurrencySymbol);
                TempData["SuccessMessage"] = _L.T(
                    $"Créance de {debt.ClientName} ({formatted}) marquée comme réglée.",
                    $"Receivable from {debt.ClientName} ({formatted}) marked as paid.");
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
                TempData["SuccessMessage"] = _L.T("Créance supprimée.", "Receivable deleted.");
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
