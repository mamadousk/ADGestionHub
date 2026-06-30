using Microsoft.AspNetCore.Mvc;

namespace AdGestionHub.Controllers
{
    public class InfosController : Controller
    {
        public IActionResult MentionsLegales() => View();
        public IActionResult CGU() => View();
        public IActionResult Confidentialite() => View();
        public IActionResult Cookies() => View();
    }
}