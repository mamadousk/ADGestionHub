using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdGestionHub.Controllers;

[AllowAnonymous]
public sealed class InfosController : Controller
{
    public IActionResult MentionsLegales() => View();
    public IActionResult CGU() => View();
    public IActionResult Confidentialite() => View();
    public IActionResult Cookies() => View();

    [HttpGet("Infos/Confidentialité")]
    public IActionResult ConfidentialiteAccentuee() => RedirectToActionPermanent(nameof(Confidentialite));
}
