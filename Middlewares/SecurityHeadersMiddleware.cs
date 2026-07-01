// ==================== FILE: Middlewares/SecurityHeadersMiddleware.cs ====================
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AdGestionHub.Middlewares
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Ajout des headers de sécurité pour se protéger contre les attaques courantes

            // 1. X-Content-Type-Options : empêche le MIME sniffing
            if (!context.Response.Headers.ContainsKey("X-Content-Type-Options"))
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            }

            // 2. X-Frame-Options : protège contre le Clickjacking (interdit l'affichage en iframe)
            if (!context.Response.Headers.ContainsKey("X-Frame-Options"))
            {
                context.Response.Headers["X-Frame-Options"] = "DENY";
            }

            // 3. Referrer-Policy : contrôle les informations de provenance envoyées
            if (!context.Response.Headers.ContainsKey("Referrer-Policy"))
            {
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            }

            // 4. Permissions-Policy : désactive les fonctionnalités sensibles du navigateur
            if (!context.Response.Headers.ContainsKey("Permissions-Policy"))
            {
                context.Response.Headers["Permissions-Policy"] =
                    "geolocation=(), microphone=(), camera=(), payment=(), usb=(), interest-cohort=()";
            }

            // 5. Content-Security-Policy (CSP) de base : autorise les ressources locales et les CDN
            // Niveaux de sécurité progressifs : on autorise tout ce qui vient du même domaine et les CDN connus.
            if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
            {
                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdnjs.cloudflare.com https://stackpath.bootstrapcdn.com https://code.jquery.com; " +
                    "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://fonts.googleapis.com; " +
                    "font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com; " +
                    "img-src 'self' data: https:; " +
                    "connect-src 'self'; " +
                    "frame-ancestors 'none';";
            }

            // 6. Cross-Origin-Resource-Policy
            if (!context.Response.Headers.ContainsKey("Cross-Origin-Resource-Policy"))
            {
                context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
            }

            await _next(context);
        }
    }
}