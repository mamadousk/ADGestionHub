using AdGestionHub.Models;
using AdGestionHub.Models.ViewModels;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdGestionHub.Controllers
{
    [Authorize(Policy = "ManageUsers")]
    public class UsersController : Controller
    {
        // Liste blanche stricte côté serveur : un Admin de boutique ne peut JAMAIS attribuer Admin ou SuperAdmin.
        private static readonly string[] AllowedStaffRoles = { "Employé" };

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IBoutiqueService _boutiqueService;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IBoutiqueService boutiqueService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _boutiqueService = boutiqueService;
        }

        // GET: /Users/
        public async Task<IActionResult> Index()
        {
            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                if (User.IsInRole("SuperAdmin"))
                    return RedirectToAction("Index", "SuperAdmin");

                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return View(new List<UserViewModel>());
            }

            var users = await _userManager.Users
                .AsNoTracking()
                .Where(u => u.BoutiqueId == currentBoutiqueId.Value)
                .ToListAsync();

            var userViewModels = new List<UserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("SuperAdmin") || roles.Contains("Admin"))
                    continue;

                userViewModels.Add(new UserViewModel
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    UserName = user.UserName ?? string.Empty,
                    FullName = user.FullName,
                    Role = roles.FirstOrDefault() ?? "Employé"
                });
            }

            return View(userViewModels);
        }

        // GET: /Users/Create
        public async Task<IActionResult> Create()
        {
            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Roles = await GetAllowedRolesListAsync();
            return View(new CreateUserViewModel { Role = "Employé" });
        }

        // POST: /Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return RedirectToAction(nameof(Index));
            }

            // Sécurité anti-escalade de privilèges : valider le rôle contre la liste blanche serveur
            if (string.IsNullOrWhiteSpace(model.Role) || !AllowedStaffRoles.Contains(model.Role, StringComparer.Ordinal))
            {
                ModelState.AddModelError(nameof(model.Role), "Rôle non autorisé.");
            }

            if (ModelState.IsValid)
            {
                var cleanEmail = model.Email.Trim();
                var cleanUserName = model.UserName.Trim();

                var existingByEmail = await _userManager.FindByEmailAsync(cleanEmail);
                if (existingByEmail != null)
                {
                    ModelState.AddModelError(nameof(model.Email), "Cet email est déjà utilisé.");
                    ViewBag.Roles = await GetAllowedRolesListAsync();
                    return View(model);
                }

                var existingByName = await _userManager.FindByNameAsync(cleanUserName);
                if (existingByName != null)
                {
                    ModelState.AddModelError(nameof(model.UserName), "Ce nom d'utilisateur est déjà pris.");
                    ViewBag.Roles = await GetAllowedRolesListAsync();
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = cleanUserName,
                    Email = cleanEmail,
                    FullName = string.IsNullOrWhiteSpace(model.FullName) ? null : model.FullName.Trim(),
                    EmailConfirmed = true,
                    BoutiqueId = currentBoutiqueId.Value
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    if (!await _roleManager.RoleExistsAsync(model.Role))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(model.Role));
                    }

                    await _userManager.AddToRoleAsync(user, model.Role);

                    TempData["SuccessMessage"] = $"Le compte employé « {user.UserName} » a été créé avec succès.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.Roles = await GetAllowedRolesListAsync();
            return View(model);
        }

        // GET: /Users/Edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.BoutiqueId == currentBoutiqueId.Value);

            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("SuperAdmin") || roles.Contains("Admin"))
                return Forbid();

            var model = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                FullName = user.FullName,
                Role = roles.FirstOrDefault() ?? "Employé"
            };

            ViewBag.Roles = await GetAllowedRolesListAsync();
            return View(model);
        }

        // POST: /Users/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, EditUserViewModel model)
        {
            if (id != model.Id) return NotFound();

            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return RedirectToAction(nameof(Index));
            }

            // Sécurité anti-escalade de privilèges : valider le rôle côté serveur
            if (string.IsNullOrWhiteSpace(model.Role) || !AllowedStaffRoles.Contains(model.Role, StringComparer.Ordinal))
            {
                ModelState.AddModelError(nameof(model.Role), "Rôle non autorisé.");
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.Id == id && u.BoutiqueId == currentBoutiqueId.Value);

                if (user == null) return NotFound();

                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.Contains("SuperAdmin") || currentRoles.Contains("Admin"))
                    return Forbid();

                var cleanEmail = model.Email.Trim();
                var cleanUserName = model.UserName.Trim();

                var emailOwner = await _userManager.FindByEmailAsync(cleanEmail);
                if (emailOwner != null && emailOwner.Id != user.Id)
                {
                    ModelState.AddModelError(nameof(model.Email), "Cet email est déjà utilisé par un autre compte.");
                    ViewBag.Roles = await GetAllowedRolesListAsync();
                    return View(model);
                }

                var userNameOwner = await _userManager.FindByNameAsync(cleanUserName);
                if (userNameOwner != null && userNameOwner.Id != user.Id)
                {
                    ModelState.AddModelError(nameof(model.UserName), "Ce nom d'utilisateur est déjà pris.");
                    ViewBag.Roles = await GetAllowedRolesListAsync();
                    return View(model);
                }

                user.Email = cleanEmail;
                user.UserName = cleanUserName;
                user.FullName = string.IsNullOrWhiteSpace(model.FullName) ? null : model.FullName.Trim();

                if (!currentRoles.Contains(model.Role))
                {
                    if (currentRoles.Any())
                        await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, model.Role);
                }

                if (!string.IsNullOrWhiteSpace(model.NewPassword))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var resetResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                    if (!resetResult.Succeeded)
                    {
                        foreach (var error in resetResult.Errors)
                            ModelState.AddModelError(string.Empty, error.Description);
                        ViewBag.Roles = await GetAllowedRolesListAsync();
                        return View(model);
                    }
                }

                var updateResult = await _userManager.UpdateAsync(user);
                if (updateResult.Succeeded)
                {
                    await _userManager.UpdateSecurityStampAsync(user);
                    TempData["SuccessMessage"] = $"L'employé « {user.UserName} » a été mis à jour.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.Roles = await GetAllowedRolesListAsync();
            return View(model);
        }

        // GET: /Users/Delete/{id}
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && u.BoutiqueId == currentBoutiqueId.Value);

            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("SuperAdmin") || roles.Contains("Admin"))
                return Forbid();

            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == id)
            {
                TempData["ErrorMessage"] = "Vous ne pouvez pas supprimer votre propre compte.";
                return RedirectToAction(nameof(Index));
            }

            var model = new UserViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Role = roles.FirstOrDefault() ?? "Employé"
            };

            return View(model);
        }

        // POST: /Users/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.BoutiqueId == currentBoutiqueId.Value);

            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("SuperAdmin") || roles.Contains("Admin"))
                return Forbid();

            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == id)
            {
                TempData["ErrorMessage"] = "Action interdite : vous ne pouvez pas supprimer votre propre compte.";
                return RedirectToAction(nameof(Index));
            }

            await _userManager.UpdateSecurityStampAsync(user);
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
                TempData["SuccessMessage"] = $"L'employé « {user.UserName} » a été supprimé.";
            else
                TempData["ErrorMessage"] = "Erreur lors de la suppression du compte employé.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<string>> GetAllowedRolesListAsync()
        {
            foreach (var roleName in AllowedStaffRoles)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
            return AllowedStaffRoles.ToList();
        }
    }
}
