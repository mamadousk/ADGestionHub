using AngleSharp.Html.Parser;
using Ganss.Xss;
using Microsoft.AspNetCore.Html;

namespace AdGestionHub.Services;

public static class PublicPopupHtml
{
    public const int MaxInputLength = 6000;
    private const int MaxStoredLength = 16000;
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    public static string? Prepare(string? html)
    {
        if (string.IsNullOrWhiteSpace(html) || html.Length > MaxInputLength) return null;
        var sanitized = Sanitize(html);
        if (sanitized.Length == 0 || sanitized.Length > MaxStoredLength) return null;

        using var document = new HtmlParser().ParseDocument(sanitized);
        return string.IsNullOrWhiteSpace(document.Body?.TextContent) ? null : sanitized;
    }

    // Safe only as the content of a div. Apply the same allowlist at the render boundary.
    public static IHtmlContent Render(string html) => new HtmlString(Sanitize(html));

    private static string Sanitize(string html) => Sanitizer.SanitizeFragment(html, "div").Trim();

    private static HtmlSanitizer CreateSanitizer()
    {
        var options = HtmlSanitizerOptions.Empty();
        options.AllowedTags.UnionWith(["a", "b", "blockquote", "br", "code", "div", "em", "h2", "h3", "h4", "hr", "i", "li", "ol", "p", "span", "strong", "ul"]);
        options.AllowedAttributes.Add("href");
        options.UriAttributes.Add("href");
        options.AllowedSchemes.Add(Uri.UriSchemeHttps);

        var sanitizer = new HtmlSanitizer(options) { KeepChildNodes = false };
        sanitizer.FilterUrl += (_, args) =>
        {
            var link = args.SanitizedUrl?.Trim();
            if (args.Tag.LocalName != "a" || string.IsNullOrEmpty(link) || link.Any(c => char.IsControl(c) || c == '\\'))
            {
                args.SanitizedUrl = null;
                return;
            }

            var local = link.StartsWith('/') && !link.StartsWith("//", StringComparison.Ordinal);
            var https = link.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Host.Length > 0 && uri.UserInfo.Length == 0;
            args.SanitizedUrl = local || https ? link : null;
        };
        return sanitizer;
    }
}
