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
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IBoutiqueService _boutiqueService;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IBoutiqueService boutiqueService)
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
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return View(new List<UserViewModel>());
            }

            // Récupérer tous les utilisateurs de la même boutique
            var users = await _userManager.Users
                .Where(u => u.BoutiqueId == currentBoutiqueId)
                .ToListAsync();

            var userViewModels = new List<UserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                // On n'affiche que les utilisateurs ayant le rôle "Employé" ou "User" (pas Admin, SuperAdmin)
                // Mais on peut aussi afficher les Admins si on veut (mais pour la confidentialité, on limite)
                // Ici, on décide de n'afficher que les employés (rôle "Employé") pour une gestion propre.
                // Vous pouvez ajuster selon vos besoins.
                if (!roles.Contains("Employé") && !roles.Contains("User"))
                    continue;

                userViewModels.Add(new UserViewModel
                {
                    Id = user.Id,
                    Email = user.Email,
                    UserName = user.UserName,
                    Role = roles.FirstOrDefault() ?? "Aucun rôle"
                });
            }

            return View(userViewModels);
        }

        // GET: /Users/Create
        [Authorize(Policy = "ManageUsers")]
        public async Task<IActionResult> Create()
        {
            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return RedirectToAction(nameof(Index));
            }

            // Récupérer les rôles disponibles (sauf SuperAdmin et Admin pour éviter de créer des admins)
            var roles = await _roleManager.Roles
                .Where(r => r.Name != "SuperAdmin" && r.Name != "Admin")
                .Select(r => r.Name)
                .ToListAsync();

            ViewBag.Roles = roles;
            ViewBag.BoutiqueId = currentBoutiqueId; // Pour passer à la vue (hidden)
            return View();
        }

        // POST: /Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "ManageUsers")]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                // Vérifier si l'email ou le nom d'utilisateur existe déjà
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Cet email est déjà utilisé.");
                    ViewBag.Roles = await GetRolesList();
                    ViewBag.BoutiqueId = currentBoutiqueId;
                    return View(model);
                }

                existingUser = await _userManager.FindByNameAsync(model.UserName);
                if (existingUser != null)
                {
                    ModelState.AddModelError("UserName", "Ce nom d'utilisateur est déjà pris.");
                    ViewBag.Roles = await GetRolesList();
                    ViewBag.BoutiqueId = currentBoutiqueId;
                    return View(model);
                }

                // Créer l'utilisateur avec BoutiqueId
                var user = new ApplicationUser
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    EmailConfirmed = true,
                    BoutiqueId = currentBoutiqueId // Assigner à la boutique de l'admin
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(model.Role))
                    {
                        var roleExists = await _roleManager.RoleExistsAsync(model.Role);
                        if (roleExists)
                            await _userManager.AddToRoleAsync(user, model.Role);
                    }

                    TempData["SuccessMessage"] = $"L'employé {user.UserName} a été créé avec succès.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.Roles = await GetRolesList();
            ViewBag.BoutiqueId = currentBoutiqueId;
            return View(model);
        }

        // GET: /Users/Edit/{id}
        [Authorize(Policy = "ManageUsers")]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.BoutiqueId == currentBoutiqueId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            // Empêcher la modification d'un SuperAdmin ou Admin (si on ne gère que les employés)
            if (roles.Contains("SuperAdmin") || roles.Contains("Admin"))
                return Forbid();

            var model = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                Role = roles.FirstOrDefault() ?? string.Empty
            };

            ViewBag.Roles = await GetRolesList();
            ViewBag.BoutiqueId = currentBoutiqueId;
            return View(model);
        }

        // POST: /Users/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "ManageUsers")]
        public async Task<IActionResult> Edit(string id, EditUserViewModel model)
        {
            if (id != model.Id) return NotFound();

            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.BoutiqueId == currentBoutiqueId);
                if (user == null) return NotFound();

                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.Contains("SuperAdmin") || currentRoles.Contains("Admin"))
                    return Forbid();

                user.Email = model.Email;
                user.UserName = model.UserName;

                // Mise à jour du rôle
                if (!string.IsNullOrEmpty(model.Role) && !currentRoles.Contains(model.Role))
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, model.Role);
                }

                // Changement de mot de passe
                if (!string.IsNullOrEmpty(model.NewPassword))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var resetResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                    if (!resetResult.Succeeded)
                    {
                        foreach (var error in resetResult.Errors)
                            ModelState.AddModelError(string.Empty, error.Description);
                        ViewBag.Roles = await GetRolesList();
                        ViewBag.BoutiqueId = currentBoutiqueId;
                        return View(model);
                    }
                }

                var updateResult = await _userManager.UpdateAsync(user);
                if (updateResult.Succeeded)
                {
                    TempData["SuccessMessage"] = $"L'employé {user.UserName} a été mis à jour.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.Roles = await GetRolesList();
            ViewBag.BoutiqueId = currentBoutiqueId;
            return View(model);
        }

        // GET: /Users/Delete/{id}
        [Authorize(Policy = "ManageUsers")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.BoutiqueId == currentBoutiqueId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("SuperAdmin") || roles.Contains("Admin"))
                return Forbid();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser.Id == id)
            {
                TempData["ErrorMessage"] = "Vous ne pouvez pas supprimer votre propre compte.";
                return RedirectToAction(nameof(Index));
            }

            var model = new UserViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                Role = roles.FirstOrDefault() ?? "Aucun rôle"
            };

            return View(model);
        }

        // POST: /Users/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "ManageUsers")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var currentBoutiqueId = await _boutiqueService.GetCurrentBoutiqueIdAsync();
            if (currentBoutiqueId == null)
            {
                TempData["ErrorMessage"] = "Aucune boutique associée à votre compte.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.BoutiqueId == currentBoutiqueId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("SuperAdmin") || roles.Contains("Admin"))
                return Forbid();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser.Id == id)
            {
                TempData["ErrorMessage"] = "Action interdite : vous ne pouvez pas supprimer votre propre compte.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
                TempData["SuccessMessage"] = $"L'employé {user.UserName} a été supprimé.";
            else
                TempData["ErrorMessage"] = "Erreur lors de la suppression.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<string>> GetRolesList()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            var roleNames = roles.Select(r => r.Name).ToList();

            // Filtrer pour ne proposer que "Employé" ou "User" (pas Admin/SuperAdmin)
            return roleNames.Where(r => r != "SuperAdmin" && r != "Admin").ToList();
        }
    }
}