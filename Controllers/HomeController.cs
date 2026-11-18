using System.Diagnostics;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdGestionHub.Controllers;

[Authorize]
public sealed class HomeController(IBoutiqueService boutiques, ITenantContextService tenant, BusinessReportService reports) : Controller
{
    [AllowAnonymous]
    public IActionResult Welcome() => User.Identity?.IsAuthenticated == true ? RedirectToAction(nameof(Index)) : View();

    [Authorize(Policy = "Dashboard")]
    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string? currency)
    {
        if (User.IsInRole("SuperAdmin")) return RedirectToAction("Index", "SuperAdmin");
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var settings = (await tenant.GetContextAsync()).Settings;
        ReportPeriod period;
        try { period = ReportPeriod.Current(startDate, endDate); }
        catch (CommerceRuleException error)
        {
            ModelState.AddModelError("", error.Message);
            period = ReportPeriod.Current(null, null);
        }
        return View(await reports.BuildAsync(shop, settings, period,
            Money.IsMvpCurrency(currency) ? currency! : settings.PrimaryCurrencyCode, User.IsInRole("Admin")));
    }

    [AllowAnonymous]
    public IActionResult Privacy() => RedirectToAction("Confidentialite", "Infos");

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
