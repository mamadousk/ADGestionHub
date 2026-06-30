using AdGestionHub.Data;
using AdGestionHub.Models;
using Microsoft.AspNetCore.Http;
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

        public async Task InvokeAsync(HttpContext httpContext, ApplicationDbContext context)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                var log = new ErrorLog
                {
                    Message = ex.Message,
                    StackTrace = ex.StackTrace,
                    ControllerName = httpContext.Request.RouteValues["controller"]?.ToString() ?? "Unknown",
                    ActionName = httpContext.Request.RouteValues["action"]?.ToString() ?? "Unknown",
                    UserEmail = httpContext.User.Identity?.Name ?? "Anonyme",
                    CreatedAt = DateTime.Now
                };

                context.ErrorLogs.Add(log);
                await context.SaveChangesAsync();
                throw;
            }
        }
    }
}