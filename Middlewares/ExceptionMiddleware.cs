using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdGestionHub.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger,
            IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception non gérée sur {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

                try
                {
                    await using var scope = httpContext.RequestServices.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    int? boutiqueId = null;
                    if (httpContext.User.Identity?.IsAuthenticated == true)
                    {
                        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
                        BoutiqueId = boutiqueId,
                        CreatedAt = DateTime.Now
                    };

                    context.ErrorLogs.Add(log);
                    await context.SaveChangesAsync();
                }
                catch (Exception logEx)
                {
                    // Ne jamais masquer l'exception originale si la base de données est inaccessible.
                    _logger.LogWarning(logEx, "Impossible d'enregistrer l'erreur dans la table ErrorLogs.");
                }

                if (!httpContext.Response.HasStarted &&
                    string.Equals(httpContext.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
                {
                    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    httpContext.Response.ContentType = "application/json; charset=utf-8";

                    var clientMessage = _env.IsDevelopment()
                        ? ex.Message
                        : "Une erreur interne est survenue. Veuillez réessayer plus tard.";

                    var payload = JsonSerializer.Serialize(new { error = clientMessage });
                    await httpContext.Response.WriteAsync(payload);
                    return;
                }

                throw;
            }
        }
    }
}
