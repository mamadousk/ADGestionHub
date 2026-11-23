using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace AdGestionHub.Services;

/// <summary>
/// Ticket temporaire (cookie protégé par Data Protection) entre la demande d'un code
/// de connexion par e-mail et la saisie de ce code.
/// </summary>
public sealed class EmailLoginTicket
{
    private const string Purpose = "AdGestionHub.EmailLogin.v1";
    private const string CookieName = "AdGestionHub.EmailLogin";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly IDataProtector _protector;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EmailLoginTicket(IDataProtectionProvider provider, IHttpContextAccessor httpContextAccessor)
    {
        _protector = provider.CreateProtector(Purpose);
        _httpContextAccessor = httpContextAccessor;
    }

    public void Set(string userId, string email, bool rememberMe)
    {
        var context = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Aucun contexte HTTP disponible pour le ticket de connexion par e-mail.");

        var payload = new EmailLoginPayload
        {
            UserId = userId,
            Email = email,
            RememberMe = rememberMe,
            ExpiresAt = DateTimeOffset.UtcNow.Add(Lifetime)
        };

        context.Response.Cookies.Append(CookieName, _protector.Protect(JsonSerializer.Serialize(payload)), new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = payload.ExpiresAt,
            IsEssential = true
        });
    }

    public EmailLoginPayload? Read()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null || !context.Request.Cookies.TryGetValue(CookieName, out var value))
            return null;

        try
        {
            var payload = JsonSerializer.Deserialize<EmailLoginPayload>(_protector.Unprotect(value));
            if (payload == null || payload.ExpiresAt <= DateTimeOffset.UtcNow)
                return null;
            return payload;
        }
        catch (Exception error) when (error is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }

    public void Clear()
    {
        _httpContextAccessor.HttpContext?.Response.Cookies.Delete(CookieName);
    }
}

public sealed class EmailLoginPayload
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
