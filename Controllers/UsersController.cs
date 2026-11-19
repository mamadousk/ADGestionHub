using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Controllers;

[Authorize(Policy = "ManageUsers")]
public sealed class UsersController(UserManager<ApplicationUser> users, IBoutiqueService boutiques, CommerceWriter writer) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var accounts = await users.Users.AsNoTracking().Where(user => user.BoutiqueId == shop)
            .OrderBy(user => user.UserName).ToListAsync();
        var staff = new List<UserViewModel>();
        foreach (var user in accounts)
            if (await IsEmployeeAsync(user)) staff.Add(ToRow(user));
        return View(staff);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int) return Forbid();
        return View(new CreateUserViewModel { Permissions = StaffAccessRules.Codes(StaffAccessRules.Cashier).ToList() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var permissions = ValidateAccess(model);
        if (!ModelState.IsValid || !await CheckUniqueAsync(model)) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.UserName.Trim(), Email = model.Email.Trim(), FullName = CommerceOperations.Clean(model.FullName),
            EmailConfirmed = true, BoutiqueId = shop, StaffAccessMask = (int)permissions
        };
        try
        {
            await writer.ExecuteAsync(shop, async () =>
            {
                RequireSuccess(await users.CreateAsync(user, model.Password));
                RequireSuccess(await users.AddToRoleAsync(user, StaffAccessRules.EmployeeRole));
                writer.Audit(shop, $"Compte employé créé : {user.UserName}", $"Accès : {AccessLabel(permissions)}");
                return true;
            });
            TempData["SuccessMessage"] = $"Le compte employé « {user.UserName} » a été créé.";
            return RedirectToAction(nameof(Index));
        }
        catch (CommerceRuleException error) { ModelState.AddModelError(string.Empty, error.Message); }
        catch (CommerceNotFoundException) { return Forbid(); }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "Ce compte n’a pas pu être créé. Vérifiez l’identifiant et l’e-mail."); }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var user = await FindEmployeeAsync(shop, id);
        return user == null ? NotFound() : View(new EditUserViewModel
        {
            Id = user.Id, UserName = user.UserName ?? "", Email = user.Email ?? "", FullName = user.FullName,
            Permissions = StaffAccessRules.Codes(StaffAccessRules.FromStored(user.StaffAccessMask)).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] string id, EditUserViewModel model)
    {
        if (id != model.Id) return NotFound();
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var user = await FindEmployeeAsync(shop, id);
        if (user == null) return NotFound();
        var permissions = ValidateAccess(model);
        if (!ModelState.IsValid || !await CheckUniqueAsync(model, user.Id)) return View(model);

        try
        {
            var before = StaffAccessRules.FromStored(user.StaffAccessMask);
            await writer.ExecuteAsync(shop, async () =>
            {
                var email = model.Email.Trim();
                var username = model.UserName.Trim();
                var identityChanged = email != user.Email || username != user.UserName;
                user.Email = email;
                user.UserName = username;
                user.FullName = CommerceOperations.Clean(model.FullName);
                user.StaffAccessMask = (int)permissions;
                if (!string.IsNullOrWhiteSpace(model.NewPassword))
                {
                    var token = await users.GeneratePasswordResetTokenAsync(user);
                    RequireSuccess(await users.ResetPasswordAsync(user, token, model.NewPassword));
                }
                else if (identityChanged) RequireSuccess(await users.UpdateSecurityStampAsync(user));
                else RequireSuccess(await users.UpdateAsync(user));
                // The access mask is read anew on each request, including for signed-in employees.
                writer.Audit(shop, $"Compte employé modifié : {user.UserName}",
                    before == permissions ? null : $"Accès avant : {AccessLabel(before)} · Après : {AccessLabel(permissions)}");
                return true;
            });
            TempData["SuccessMessage"] = $"L’employé « {user.UserName} » a été mis à jour.";
            return RedirectToAction(nameof(Index));
        }
        catch (CommerceRuleException error) { ModelState.AddModelError(string.Empty, error.Message); }
        catch (CommerceNotFoundException) { return Forbid(); }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "Ce compte n’a pas pu être mis à jour."); }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var user = await FindEmployeeAsync(shop, id);
        return user == null ? NotFound() : View(ToRow(user));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        if (await boutiques.GetCurrentBoutiqueIdAsync() is not int shop) return Forbid();
        var user = await FindEmployeeAsync(shop, id);
        if (user == null) return NotFound();
        try
        {
            await writer.ExecuteAsync(shop, async () =>
            {
                RequireSuccess(await users.DeleteAsync(user));
                writer.Audit(shop, $"Compte employé supprimé : {user.UserName}");
                return true;
            });
            TempData["SuccessMessage"] = $"L’employé « {user.UserName} » a été supprimé.";
        }
        catch (CommerceNotFoundException) { return Forbid(); }
        catch (CommerceRuleException) { TempData["ErrorMessage"] = "Ce compte n’a pas pu être supprimé."; }
        catch (DbUpdateException) { TempData["ErrorMessage"] = "Ce compte n’a pas pu être supprimé."; }
        return RedirectToAction(nameof(Index));
    }

    private async Task<ApplicationUser?> FindEmployeeAsync(int shop, string? id)
    {
        if (string.IsNullOrEmpty(id) || id == users.GetUserId(User)) return null;
        var user = await users.Users.SingleOrDefaultAsync(user => user.BoutiqueId == shop && user.Id == id);
        return user != null && await IsEmployeeAsync(user) ? user : null;
    }

    private async Task<bool> IsEmployeeAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        return roles.Count == 1 && roles.Contains(StaffAccessRules.EmployeeRole);
    }

    private StaffPermission ValidateAccess(StaffUserInput model)
    {
        if (model.Role != StaffAccessRules.EmployeeRole)
            ModelState.AddModelError(nameof(model.Role), "Seul le rôle Employé peut être attribué ici.");
        if (!StaffAccessRules.TryParse(model.Permissions, out var permissions))
            ModelState.AddModelError(nameof(model.Permissions), "Vérifiez les accès sélectionnés : les règlements nécessitent l’historique des ventes.");
        return permissions;
    }

    private async Task<bool> CheckUniqueAsync(StaffUserInput model, string? except = null)
    {
        var emailOwner = await users.FindByEmailAsync(model.Email.Trim());
        if (emailOwner != null && emailOwner.Id != except) ModelState.AddModelError(nameof(model.Email), "Cet e-mail est déjà utilisé.");
        var nameOwner = await users.FindByNameAsync(model.UserName.Trim());
        if (nameOwner != null && nameOwner.Id != except) ModelState.AddModelError(nameof(model.UserName), "Cet identifiant est déjà utilisé.");
        return ModelState.IsValid;
    }

    private static UserViewModel ToRow(ApplicationUser user) => new()
    {
        Id = user.Id, UserName = user.UserName ?? "", Email = user.Email ?? "", FullName = user.FullName,
        Permissions = StaffAccessRules.FromStored(user.StaffAccessMask)
    };

    private static string AccessLabel(StaffPermission permissions) => permissions == StaffPermission.None ? "Aucun accès métier"
        : string.Join(", ", StaffAccessRules.Features.Where(feature => permissions.Has(feature.Permission)).Select(feature => feature.Name));

    private static void RequireSuccess(IdentityResult result)
    {
        if (!result.Succeeded) throw new CommerceRuleException(string.Join(" ", result.Errors.Select(error => error.Description)));
    }
}
