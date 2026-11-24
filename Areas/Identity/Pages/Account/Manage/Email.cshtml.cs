// ==================== FILE: Areas/Identity/Pages/Account/Manage/Email.cshtml.cs ====================
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using AdGestionHub.Models;
using AdGestionHub.Services;

namespace AdGestionHub.Areas.Identity.Pages.Account.Manage
{
    public class EmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;

        public EmailModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
        }

        public string Email { get; set; }
        public bool IsEmailConfirmed { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Nouvelle adresse e-mail")]
            public string NewEmail { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var email = await _userManager.GetEmailAsync(user);
            Email = email;

            Input = new InputModel
            {
                NewEmail = email,
            };

            IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("Impossible de charger l’utilisateur connecté.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostChangeEmailAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("Impossible de charger l’utilisateur connecté.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var email = await _userManager.GetEmailAsync(user);
            if (Input.NewEmail != email)
            {
                var userId = await _userManager.GetUserIdAsync(user);
                var code = await _userManager.GenerateChangeEmailTokenAsync(user, Input.NewEmail);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmailChange",
                    pageHandler: null,
                    values: new { area = "Identity", userId = userId, email = Input.NewEmail, code = code },
                    protocol: Request.Scheme) ?? string.Empty;
                try
                {
                    await _emailSender.SendEmailAsync(
                        Input.NewEmail,
                        "Confirmez votre nouvelle adresse e-mail",
                        AccountEmails.ConfirmEmailChange(callbackUrl));
                }
                catch (Exception)
                {
                    StatusMessage = "Error: L'envoi de l'e-mail de confirmation a échoué. Réessayez dans quelques instants.";
                    return RedirectToPage();
                }

                StatusMessage = "Un e-mail de confirmation a été envoyé à votre nouvelle adresse.";
                return RedirectToPage();
            }

            StatusMessage = "Votre adresse e-mail n'a pas changé.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSendVerificationEmailAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("Impossible de charger l’utilisateur connecté.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var userId = await _userManager.GetUserIdAsync(user);
            var email = await _userManager.GetEmailAsync(user) ?? string.Empty;
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = userId, code = code },
                protocol: Request.Scheme) ?? string.Empty;
            try
            {
                await _emailSender.SendEmailAsync(
                    email,
                    "Confirmez votre adresse e-mail",
                    AccountEmails.ConfirmEmail(callbackUrl));
            }
            catch (Exception)
            {
                StatusMessage = "Error: L'envoi de l'e-mail de vérification a échoué. Réessayez dans quelques instants.";
                return RedirectToPage();
            }

            StatusMessage = "Un e-mail de vérification a été envoyé. Vérifiez votre boîte de réception.";
            return RedirectToPage();
        }
    }
}