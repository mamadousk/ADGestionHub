using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

[Authorize(Policy = "ShopMember")]
public sealed class OnboardingController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    IBoutiqueService boutiques, CommerceWriter writer) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Terms()
    {
        var shop = await boutiques.GetCurrentBoutiqueAsync();
        var userId = users.GetUserId(User);
        if (shop == null || userId == null) return Forbid();
        return await HasAcceptedAsync(userId) ? AfterTerms(shop) : View();
    }

    [HttpPost]
    public async Task<IActionResult> Terms([FromForm] bool acceptTerms, [FromForm] string? version)
    {
        var shop = await boutiques.GetCurrentBoutiqueAsync();
        var userId = users.GetUserId(User);
        if (shop == null || userId == null) return Forbid();
        if (!string.Equals(version, LegalPublication.TermsVersion, StringComparison.Ordinal))
            ModelState.AddModelError("", "La version des conditions a changé. Lisez et acceptez la version affichée ci-dessous.");
        else if (!acceptTerms)
            ModelState.AddModelError("", "Cochez la case pour accepter les conditions d’utilisation et continuer.");
        if (!ModelState.IsValid) return View();

        try
        {
            await writer.ExecuteAsync(shop.Id, async () =>
            {
                if (!await db.Users.AnyAsync(u => u.Id == userId && u.BoutiqueId == shop.Id, HttpContext.RequestAborted))
                    throw new CommerceNotFoundException();
                if (!await HasAcceptedAsync(userId))
                    db.TermsAcceptances.Add(new TermsAcceptance { UserId = userId, Version = LegalPublication.TermsVersion, AcceptedAtUtc = DateTimeOffset.UtcNow });
                return true;
            });
        }
        catch (CommerceNotFoundException) { return NotFound(); }
        TempData["SuccessMessage"] = $"Conditions d’utilisation acceptées (version {LegalPublication.TermsVersion}).";
        return AfterTerms(shop);
    }

    private Task<bool> HasAcceptedAsync(string userId) => db.TermsAcceptances
        .AnyAsync(a => a.UserId == userId && a.Version == LegalPublication.TermsVersion, HttpContext.RequestAborted);

    private IActionResult AfterTerms(Boutique shop) => shop.SetupPending && User.IsInRole("Admin")
        ? RedirectToAction("Setup", "Settings") : RedirectToAction("Index", "Home");
}
