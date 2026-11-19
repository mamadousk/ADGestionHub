using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AdGestionHub.Services;

internal static class AccountNavigation
{
    // A fixed origin is only used to parse and normalize a local URL, never for a redirect.
    private static readonly Uri ParsingOrigin = new("https://adgestion.invalid/");

    public static string AfterSignIn(IUrlHelper url, string? returnUrl)
    {
        var home = url.Action("Index", "Home", new { area = "" }) ?? url.Content("~/")!;
        if (!url.IsLocalUrl(returnUrl)) return home;
        var local = url.Content(returnUrl)!;
        if (!Uri.TryCreate(ParsingOrigin, local, out var address) || !ParsingOrigin.IsBaseOf(address)) return home;

        var path = PathString.FromUriComponent(address.AbsolutePath);
        var pathBase = url.ActionContext.HttpContext.Request.PathBase;
        if (pathBase.HasValue)
        {
            if (!path.StartsWithSegments(pathBase, out var remaining)) return home;
            path = remaining;
        }

        var segments = (path.Value ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
        bool Is(int index, string value) => index < segments.Length && segments[index].Equals(value, StringComparison.OrdinalIgnoreCase);

        // Resume at the HTML view; downloads must be explicitly requested after sign-in.
        if (Is(0, "Reports") && Is(1, "Export"))
        {
            var query = QueryHelpers.ParseQuery(address.Query);
            return url.Action("Index", "Reports", new
            {
                area = "", startDate = query.GetValueOrDefault("startDate").FirstOrDefault(),
                endDate = query.GetValueOrDefault("endDate").FirstOrDefault(),
                currency = query.GetValueOrDefault("currency").FirstOrDefault()
            }) ?? home;
        }
        if (Is(0, "Sales") && Is(1, "Receipt"))
        {
            var idValue = segments.Length > 2 ? segments[2] : QueryHelpers.ParseQuery(address.Query).GetValueOrDefault("id").FirstOrDefault();
            return int.TryParse(idValue, out var id) && id > 0
                ? url.Action("Receipt", "Sales", new { area = "", id }) ?? home
                : url.Action("Index", "Sales", new { area = "" }) ?? home;
        }
        if (Is(0, "Marketing") && Is(1, "ExportLeads"))
            return url.Action("AdminDashboard", "Marketing", new { area = "" }) ?? home;
        if (Is(0, "Identity") && Is(1, "Account"))
        {
            if (Is(2, "Login") || Is(2, "Logout") || Is(2, "LoginWith2fa") || Is(2, "LoginWithRecoveryCode")) return home;
            if (Is(2, "Manage") && Is(3, "DownloadPersonalData"))
                return url.Page("/Account/Manage/PersonalData", new { area = "Identity" }) ?? home;
        }

        return local;
    }
}
