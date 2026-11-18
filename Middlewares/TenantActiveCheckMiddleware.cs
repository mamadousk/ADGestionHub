using AdGestionHub.Data;
using AdGestionHub.Models;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AdGestionHub.Middlewares;

public sealed class TenantActiveCheckMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IBoutiqueService boutiques,
        SignInManager<ApplicationUser> signIn, ApplicationDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        var route = context.Request.RouteValues;
        bool Matches(string key, string value) => string.Equals(route[key]?.ToString(), value, StringComparison.OrdinalIgnoreCase);
        void GoTo(string path) => context.Response.Redirect($"{context.Request.PathBase}{path}");

        // These endpoints must remain available during onboarding and after a suspension.
        if (context.User.IsInRole("SuperAdmin") || context.GetEndpoint() == null || context.Request.Path == "/health"
            || Matches("controller", "Infos") || Matches("page", "/Account/Logout") || Matches("page", "/Account/Login")
            || Matches("page", "/Account/AccessDenied") || (Matches("controller", "Home") && Matches("action", "Error")))
        {
            await next(context);
            return;
        }

        var boutique = await boutiques.GetCurrentBoutiqueAsync();
        if (boutique?.IsActive != true)
        {
            await signIn.SignOutAsync();
            GoTo("/Identity/Account/Login?suspended=1");
            return;
        }

        // Check the stored acceptance, not a cookie or a URL parameter. Direct requests to
        // feature endpoints cannot bypass the mandatory step (including state-changing requests).
        var isTerms = Matches("controller", "Onboarding") && Matches("action", "Terms");
        if (!isTerms)
        {
            var userId = signIn.UserManager.GetUserId(context.User);
            if (!await db.TermsAcceptances.AnyAsync(a => a.UserId == userId && a.Version == LegalPublication.TermsVersion, context.RequestAborted))
            {
                GoTo("/Onboarding/Terms");
                return;
            }

            var isSetup = Matches("controller", "Settings") && (Matches("action", "Setup") || Matches("action", "SkipSetup"));
            if (boutique.SetupPending && context.User.IsInRole("Admin") && !isSetup)
            {
                GoTo("/Settings/Setup");
                return;
            }
        }

        await next(context);
    }
}
