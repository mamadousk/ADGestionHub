using System.Security.Claims;
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

[Authorize(Policy = StaffAccessRules.Expenses)]
public sealed class ExpensesController(ApplicationDbContext db, IBoutiqueService boutiques, ITenantContextService tenant,
    CommerceWriter writer, OperationJournal journal) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var expenses = await db.Expenses.AsNoTracking().Where(e => e.BoutiqueId == shop).OrderByDescending(e => e.Date).ToListAsync();
        if (User.IsInRole("Admin")) ViewData["OperationAuthors"] = await journal.AuthorsAsync(shop, expenses.Select(e => e.UserId), HttpContext.RequestAborted);
        return View(expenses);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var expense = await FindAsync(shop, id);
        if (expense == null) return NotFound();
        if (User.IsInRole("Admin")) ViewData["OperationAuthors"] = await journal.AuthorsAsync(shop, [expense.UserId], HttpContext.RequestAborted);
        return View(expense);
    }

    [HttpGet]
    public async Task<IActionResult> Create() => View("Form", new ExpenseFormViewModel
    {
        CurrencyCode = (await tenant.GetContextAsync()).PrimaryCurrency.Code
    });

    [HttpPost]
    public Task<IActionResult> Create(ExpenseFormViewModel model) => SaveAsync(null, model);

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var expense = await FindAsync(shop, id);
        if (expense == null) return NotFound();
        ViewData["ExpenseId"] = id;
        return View("Form", new ExpenseFormViewModel
        {
            Category = expense.Category, Amount = expense.Amount, Date = expense.Date, Description = expense.Description,
            PaymentMethod = expense.PaymentMethod ?? "Espèces", CurrencyCode = expense.CurrencyCode
        });
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public Task<IActionResult> Edit(int id, ExpenseFormViewModel model) => SaveAsync(id, model);

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        try
        {
            await writer.ExecuteAsync(shop, async () =>
            {
                var expense = await db.Expenses.SingleOrDefaultAsync(e => e.BoutiqueId == shop && e.Id == id) ?? throw new CommerceNotFoundException();
                await writer.AuditAsync(shop, $"Suppression de la dépense #{id}", $"{expense.Description} | {Money.Display(expense.Amount, expense.CurrencyCode)} | {expense.Date:dd/MM/yyyy}");
                db.Expenses.Remove(expense);
                return true;
            });
            TempData["SuccessMessage"] = "Dépense supprimée. La correction a été journalisée.";
            return RedirectToAction(nameof(Index));
        }
        catch (CommerceNotFoundException) { return NotFound(); }
    }

    private Task<Expense?> FindAsync(int shop, int id) => db.Expenses.AsNoTracking().SingleOrDefaultAsync(e => e.BoutiqueId == shop && e.Id == id);

    private async Task<IActionResult> SaveAsync(int? id, ExpenseFormViewModel model)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                var expense = await writer.ExecuteAsync(shop, async () =>
                {
                    var settings = await writer.SettingsAsync(shop);
                    var currency = model.CurrencyCode;
                    if (model.Date == default || model.Date.Date > DateTime.Today)
                        throw new CommerceRuleException("Indiquez une date de dépense non future.");
                    string? before = null;
                    Expense row;
                    if (id.HasValue)
                    {
                        row = await db.Expenses.SingleOrDefaultAsync(e => e.BoutiqueId == shop && e.Id == id) ?? throw new CommerceNotFoundException();
                        if (currency != row.CurrencyCode) throw new CommerceRuleException("La devise historique de cette dépense ne peut pas être modifiée.");
                        before = $"Avant : {Money.Display(row.Amount, currency)} | {row.Date:dd/MM/yyyy}";
                    }
                    else
                    {
                        if (!Money.IsMvpCurrency(currency) || !Money.AvailableCurrencies(settings).Contains(currency))
                            throw new CommerceRuleException("Choisissez une devise configurée pour votre entreprise.");
                        var info = CountryProfileCatalog.GetCurrency(currency);
                        row = new Expense
                        {
                            BoutiqueId = shop, CurrencyCode = currency, CurrencySymbol = info.Symbol, CurrencyDecimals = info.Decimals,
                            CdfPerUsd = settings.CdfPerUsd, UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
                        };
                        db.Expenses.Add(row);
                    }
                    row.Date = model.Date.Date;
                    row.Description = model.Description.Trim();
                    row.Category = model.Category.Trim();
                    row.Amount = Money.Amount(model.Amount, currency);
                    row.PaymentMethod = row.PaymentMethod == model.PaymentMethod ? row.PaymentMethod : PaymentMeans.Validate(model.PaymentMethod, settings);
                    if (!id.HasValue) await db.SaveChangesAsync();
                    var details = $"{row.Description} · {Money.Display(row.Amount, currency)} · Date de la dépense : {row.Date:dd/MM/yyyy}";
                    await writer.AuditAsync(shop, id.HasValue ? $"Dépense #{id} corrigée" : "Dépense enregistrée",
                        before == null ? details : $"{before} · {details}",
                        id.HasValue ? null : OperationSubject.Expense, id.HasValue ? null : row.Id);
                    return row;
                });
                TempData["SuccessMessage"] = "Dépense enregistrée.";
                return RedirectToAction(nameof(Details), new { id = expense.Id });
            }
            catch (CommerceRuleException error) { ModelState.AddModelError("", error.Message); }
            catch (CommerceNotFoundException) { return NotFound(); }
        }
        ViewData["ExpenseId"] = id;
        return View("Form", model);
    }
}
