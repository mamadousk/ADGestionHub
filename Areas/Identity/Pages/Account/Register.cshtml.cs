using System.ComponentModel.DataAnnotations;
using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdGestionHub.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    ApplicationDbContext db, CommerceWriter writer, ILogger<RegisterModel> logger) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ReturnUrl { get; private set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Le nom de l'entreprise est obligatoire.")]
        [StringLength(150)] public string StoreName { get; set; } = "";
        [Required] [StringLength(10)] public string PrimaryCurrencyCode { get; set; } = "CDF";
        [Phone(ErrorMessage = "Numéro de téléphone invalide.")]
        [StringLength(40)] public string? PhoneNumber { get; set; }
        [Required(ErrorMessage = "L'adresse e-mail est obligatoire.")]
        [EmailAddress(ErrorMessage = "Adresse e-mail invalide.")]
        [StringLength(256)] public string Email { get; set; } = "";
        [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Utilisez au moins 8 caractères.")]
        [DataType(DataType.Password)] public string Password { get; set; } = "";
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
        public string ConfirmPassword { get; set; } = "";
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home", new { area = "" });
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home", new { area = "" });
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        if (!Money.IsMvpCurrency(Input.PrimaryCurrencyCode))
            ModelState.AddModelError("Input.PrimaryCurrencyCode", "Choisissez le CDF ou l'USD.");
        if (!ModelState.IsValid) return Page();
        var email = Input.Email.Trim();
        if (await users.FindByEmailAsync(email) != null)
        {
            ModelState.AddModelError("Input.Email", "Cet e-mail est déjà associé à un compte.");
            return Page();
        }

        ApplicationUser user;
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            try
            {
                var shop = new Boutique
                {
                    Nom = Input.StoreName.Trim(), Email = email, Telephone = Input.PhoneNumber?.Trim() ?? "", Adresse = "", SetupPending = true
                };
                db.Boutiques.Add(shop);
                await db.SaveChangesAsync();
                var settings = CommerceWriter.DefaultSettings(shop);
                settings.PrimaryCurrencyCode = Input.PrimaryCurrencyCode;
                db.StoreSettings.Add(settings);
                user = new ApplicationUser
                {
                    UserName = email, Email = email, PhoneNumber = Input.PhoneNumber, BoutiqueId = shop.Id
                };
                var created = await users.CreateAsync(user, Input.Password);
                if (!created.Succeeded)
                {
                    foreach (var error in created.Errors)
                        ModelState.AddModelError("", error.Code.StartsWith("Password", StringComparison.Ordinal)
                            ? "Le mot de passe doit contenir au moins 8 caractères, une majuscule, une minuscule, un chiffre et un caractère spécial."
                            : "Vérifiez l'adresse e-mail. Elle n'a pas pu être utilisée pour l'inscription.");
                    return Page();
                }
                if (!(await users.AddToRoleAsync(user, "Admin")).Succeeded)
                    throw new InvalidOperationException("Le rôle Admin n'a pas été attribué lors de l'inscription.");
                writer.Audit(shop.Id, "Entreprise créée et compte administrateur initialisé", email);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception error)
            {
                logger.LogError(error, "Échec de création d'entreprise.");
                ModelState.AddModelError("", "L'entreprise n'a pas pu être créée. Réessayez dans quelques instants.");
                return Page();
            }
        }
        await signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Terms", "Onboarding", new { area = "" });
    }
}
