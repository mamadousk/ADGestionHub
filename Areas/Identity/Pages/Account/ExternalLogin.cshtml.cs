using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdGestionHub.Areas.Identity.Pages.Account;

// In V1, new accounts must go through the registration that creates their company.
// Keep the endpoint override so the default Identity UI cannot register an orphan account.
[AllowAnonymous]
public sealed class ExternalLoginModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("./Login");
    public IActionResult OnGetCallback() => RedirectToPage("./Login");
    public IActionResult OnPost() => RedirectToPage("./Login");
    public IActionResult OnPostConfirmation() => RedirectToPage("./Login");
}
