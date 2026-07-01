// ==================== FILE: Middlewares/ExceptionMiddleware.cs ====================
using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace AdGestionHub.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                // Récupérer le contexte via le service provider
                var scope = httpContext.RequestServices.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Récupérer le BoutiqueId de l'utilisateur si connecté
                int? boutiqueId = null;
                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var user = await context.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u => u.Id == userId);
                        boutiqueId = user?.BoutiqueId;
                    }
                }

                var log = new ErrorLog
                {
                    Message = ex.Message,
                    StackTrace = ex.StackTrace,
                    ControllerName = httpContext.Request.RouteValues["controller"]?.ToString() ?? "Unknown",
                    ActionName = httpContext.Request.RouteValues["action"]?.ToString() ?? "Unknown",
                    UserEmail = httpContext.User.Identity?.Name ?? "Anonyme",
                    BoutiqueId = boutiqueId ?? 0,
                    CreatedAt = DateTime.Now
                };

                context.ErrorLogs.Add(log);
                await context.SaveChangesAsync();

                // Si c'est une requête AJAX, on renvoie un JSON d'erreur
                if (httpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    httpContext.Response.StatusCode = 500;
                    httpContext.Response.ContentType = "application/json";
                    await httpContext.Response.WriteAsync($"{{\"error\": \"{ex.Message}\"}}");
                    return;
                }

                // Sinon, on relance l'exception pour le middleware standard
                throw;
            }
        }
    }
}