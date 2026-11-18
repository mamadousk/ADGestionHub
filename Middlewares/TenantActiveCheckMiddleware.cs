using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System;
using System.Threading.Tasks;

namespace AdGestionHub.Middlewares
{
    /// <summary>
    /// Vérifie qu'un compte authentifié non-SuperAdmin appartient bien à une boutique active.
    /// Si le SuperAdmin suspend une boutique, les sessions existantes sont immédiatement coupées.
    /// </summary>
    public class TenantActiveCheckMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantActiveCheckMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IBoutiqueService boutiqueService,
            SignInManager<ApplicationUser> signInManager)
        {
            if (context.User.Identity?.IsAuthenticated == true)
                context.Response.Headers["Cache-Control"] = "no-store";

            if (context.User.Identity?.IsAuthenticated == true && !context.User.IsInRole("SuperAdmin"))
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var isExempt = path.StartsWith("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase)
                            || path.StartsWith("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase)
                            || path.StartsWith("/Infos", StringComparison.OrdinalIgnoreCase)
                            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);

                if (!isExempt)
                {
                    var boutique = await boutiqueService.GetCurrentBoutiqueAsync();
                    if (boutique == null || !boutique.IsActive)
                    {
                        await signInManager.SignOutAsync();
                        context.Response.Redirect("/Identity/Account/Login?suspended=1");
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}
