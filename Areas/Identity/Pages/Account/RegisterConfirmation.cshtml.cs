using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdGestionHub.Areas.Identity.Pages.Account;

// A confirmation token belongs only in a message sent to the account's address.
[AllowAnonymous]
public sealed class RegisterConfirmationModel : PageModel { }
