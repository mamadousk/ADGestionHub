using AdGestionHub.Data;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

[Authorize(Policy = "ShopMember")]
public sealed class PaymentsController(ApplicationDbContext db, IBoutiqueService boutiques, ITenantContextService tenant,
    CommerceOperations commerce) : Controller
{
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        return View(await db.Payments.AsNoTracking().Where(p => p.BoutiqueId == shop)
            .OrderByDescending(p => p.RecordedAt).ThenByDescending(p => p.Id).ToListAsync());
    }

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create() => View(new PaymentInput { CurrencyCode = (await tenant.GetContextAsync()).PrimaryCurrency.Code });

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(PaymentInput model)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                await commerce.RecordPaymentAsync(shop, "Receipt", null, model);
                TempData["SuccessMessage"] = "Encaissement enregistré.";
                return RedirectToAction(nameof(Index));
            }
            catch (CommerceRuleException error) { ModelState.AddModelError("", error.Message); }
            catch (CommerceNotFoundException) { return NotFound(); }
        }
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Settle(string target, int id, PaymentInput model)
    {
        if (target is not ("Sale" or "Purchase" or "Debt")) return NotFound();
        if (target != "Sale" && !User.IsInRole("Admin")) return Forbid();
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                await commerce.RecordPaymentAsync(shop, target, id, model);
                TempData["SuccessMessage"] = "Règlement enregistré. Le solde a été actualisé.";
            }
            catch (CommerceRuleException error) { TempData["ErrorMessage"] = error.Message; }
            catch (CommerceNotFoundException) { return NotFound(); }
        }
        else
            TempData["ErrorMessage"] = "Vérifiez le montant, la devise et le moyen de paiement.";
        return RedirectToAction("Details", target switch { "Sale" => "Sales", "Purchase" => "Purchases", _ => "Debts" }, new { id });
    }
}
