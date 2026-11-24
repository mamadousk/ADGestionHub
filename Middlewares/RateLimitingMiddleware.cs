using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AdGestionHub.Middlewares
{
    public class RateLimitingMiddleware
    {
        private sealed class RateBucket
        {
            public int Count;
            public DateTimeOffset ExpiresAt;
        }

        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private const int GeneralMutationLimit = 100;
        private const int SensitiveEndpointLimit = 15;
        private static readonly TimeSpan TimeWindow = TimeSpan.FromMinutes(1);

        public RateLimitingMiddleware(RequestDelegate next, IMemoryCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (HttpMethods.IsPost(context.Request.Method) ||
                HttpMethods.IsPut(context.Request.Method) ||
                HttpMethods.IsDelete(context.Request.Method))
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var path = context.Request.Path.Value ?? string.Empty;

                var isSensitive = path.StartsWith("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase)
                               || path.StartsWith("/Identity/Account/Register", StringComparison.OrdinalIgnoreCase)
                               || path.StartsWith("/Identity/Account/ForgotPassword", StringComparison.OrdinalIgnoreCase)
                               || path.StartsWith("/Identity/Account/ResetPassword", StringComparison.OrdinalIgnoreCase)
                               || path.StartsWith("/Marketing/", StringComparison.OrdinalIgnoreCase);

                var bucketScope = isSensitive ? "Auth" : "General";
                var maxRequests = isSensitive ? SensitiveEndpointLimit : GeneralMutationLimit;
                var key = $"RateLimit_{bucketScope}_{ipAddress}";

                var bucket = _cache.GetOrCreate(key, entry =>
                {
                    var expiresAt = DateTimeOffset.UtcNow.Add(TimeWindow);
                    entry.AbsoluteExpiration = expiresAt;
                    return new RateBucket { Count = 0, ExpiresAt = expiresAt };
                })!;

                var currentCount = Interlocked.Increment(ref bucket.Count);
                if (currentCount > maxRequests)
                {
                    var retryAfter = Math.Max(1, (int)Math.Ceiling((bucket.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds));
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.Headers["Retry-After"] = retryAfter.ToString();
                    context.Response.ContentType = "application/json; charset=utf-8";

                    var json = JsonSerializer.Serialize(new
                    {
                        error = $"Trop de requêtes. Veuillez réessayer dans {retryAfter} secondes."
                    });
                    await context.Response.WriteAsync(json);
                    return;
                }
            }

            await _next(context);
        }
    }
}
