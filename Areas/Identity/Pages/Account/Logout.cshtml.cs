// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using AdGestionHub.Models; // <-- AJOUTÉ : Pour reconnaître ApplicationUser

namespace AdGestionHub.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        // CORRECTION : Utilisation de ApplicationUser au lieu d'IdentityUser
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(SignInManager<ApplicationUser> signInManager, ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("L'utilisateur s'est déconnecté.");

            if (returnUrl != null)
            {
                return LocalRedirect(returnUrl);
            }
            else
            {
                // Redirection vers la page de login pour AdGestionHub
                return RedirectToPage("/Account/Login");
            }
        }

        // Ajout d'une méthode OnGet pour déconnecter immédiatement si on accède à la page
        public async Task<IActionResult> OnGet(string returnUrl = null)
        {
            return await OnPost(returnUrl);
        }
    }
}