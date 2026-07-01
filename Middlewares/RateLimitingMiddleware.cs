// ==================== FILE: Middlewares/RateLimitingMiddleware.cs ====================
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Net;
using System.Threading.Tasks;

namespace AdGestionHub.Middlewares
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly int _maxRequests = 100; // Nombre max de requêtes
        private readonly TimeSpan _timeWindow = TimeSpan.FromMinutes(1); // Fenêtre de temps

        public RateLimitingMiddleware(RequestDelegate next, IMemoryCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // On limite uniquement les requêtes POST, PUT, DELETE (modifications)
            if (context.Request.Method == HttpMethods.Post ||
                context.Request.Method == HttpMethods.Put ||
                context.Request.Method == HttpMethods.Delete)
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var key = $"RateLimit_{ipAddress}";

                if (_cache.TryGetValue(key, out int requestCount))
                {
                    if (requestCount >= _maxRequests)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync($"{{\"error\": \"Trop de requêtes. Veuillez réessayer dans {_timeWindow.TotalSeconds} secondes.\"}}");
                        return;
                    }

                    _cache.Set(key, requestCount + 1, _timeWindow);
                }
                else
                {
                    _cache.Set(key, 1, _timeWindow);
                }
            }

            await _next(context);
        }
    }
}