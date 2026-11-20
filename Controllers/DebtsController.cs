using AdGestionHub.Data;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

[Authorize(Roles = "Admin")]
public sealed class DebtsController(ApplicationDbContext db, IBoutiqueService boutiques, BusinessReportService reports, OperationJournal journal) : Controller
{
    public async Task<IActionResult> Index(string? currency)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var balances = await reports.OutstandingAsync(shop, DateTime.Today.AddDays(1), Money.IsMvpCurrency(currency) ? currency : null);
        ViewData["OperationAuthors"] = await journal.AuthorsAsync(shop, balances.Select(b => b.CreatedByUserId), HttpContext.RequestAborted);
        return View(balances);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var debt = await db.Debts.AsNoTracking().Include(d => d.Payments).SingleOrDefaultAsync(d => d.BoutiqueId == shop && d.Id == id);
        if (debt == null) return NotFound();
        ViewData["OperationAuthors"] = await journal.AuthorsAsync(shop, debt.Payments.Select(p => p.CreatedByUserId), HttpContext.RequestAborted);
        return View(debt);
    }
}
