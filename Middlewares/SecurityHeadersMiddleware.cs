using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace AdGestionHub.Middlewares
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _env;

        public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
        {
            _next = next;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.TryAdd(
                "Permissions-Policy",
                "geolocation=(), microphone=(), camera=(), payment=(), usb=(), interest-cohort=()");
            context.Response.Headers.TryAdd("Cross-Origin-Resource-Policy", "same-origin");

            var frameAncestors = _env.IsDevelopment() ? "" : "frame-ancestors 'none'; ";
            if (!_env.IsDevelopment())
            {
                context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
            }

            if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
            {
                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://code.jquery.com https://cdn.jsdelivr.net; " +
                    "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://fonts.googleapis.com https://code.jquery.com https://cdn.jsdelivr.net; " +
                    "font-src 'self' data: https://fonts.gstatic.com https://cdnjs.cloudflare.com; " +
                    "img-src 'self' data: https:; " +
                    "media-src 'self'; " +
                    "connect-src 'self'; " +
                    "object-src 'none'; " +
                    "base-uri 'self'; " +
                    "form-action 'self'; " +
                    frameAncestors;
            }

            await _next(context);
        }
    }
}
