using System.Net;
using System.Text;

namespace AdGestionHub.Services;

/// <summary>Modèles HTML français pour les e-mails de compte.</summary>
public static class AccountEmails
{
    public static string ConfirmEmail(string callbackUrl) => Page(
        "Confirmez votre adresse e-mail",
        "Merci d’avoir créé votre compte AD Gestion Hub. Confirmez votre adresse e-mail pour sécuriser votre compte et activer la réinitialisation du mot de passe.",
        "Confirmer mon adresse",
        callbackUrl,
        "Ce lien expire et ne peut être utilisé qu’une seule fois. Si vous n’êtes pas à l’origine de cette demande, ignorez ce message.");

    public static string ConfirmEmailChange(string callbackUrl) => Page(
        "Confirmez votre nouvelle adresse e-mail",
        "Vous avez demandé à modifier l’adresse e-mail de votre compte AD Gestion Hub. Confirmez cette nouvelle adresse pour finaliser le changement.",
        "Confirmer cette adresse",
        callbackUrl,
        "Ce lien expire et ne peut être utilisé qu’une seule fois. Si vous n’êtes pas à l’origine de cette demande, ignorez ce message et sécurisez votre compte.");

    public static string ResetPassword(string callbackUrl) => Page(
        "Réinitialisez votre mot de passe",
        "Vous avez demandé un nouveau mot de passe pour votre compte AD Gestion Hub. Choisissez-le en cliquant sur le bouton ci-dessous.",
        "Choisir un nouveau mot de passe",
        callbackUrl,
        "Ce lien expire et ne peut être utilisé qu’une seule fois. Si vous n’êtes pas à l’origine de cette demande, ignorez ce message : votre mot de passe actuel reste inchangé.");

    public static string LoginCode(string code) => CodePage(
        "Votre code de connexion",
        code,
        "Utilisez ce code pour vous connecter à AD Gestion Hub. Il expire dans quelques minutes.",
        "Ne communiquez jamais ce code, y compris à une personne qui prétend travailler pour AD Gestion Hub.");

    public static string TwoFactorCode(string code) => CodePage(
        "Votre code de double authentification",
        code,
        "Utilisez ce code pour terminer votre connexion à AD Gestion Hub. Il expire dans quelques minutes.",
        "Ne communiquez jamais ce code. Si vous n’êtes pas à l’origine de cette demande, changez immédiatement votre mot de passe.");

    private static string Page(string title, string text, string button, string url, string note)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang='fr'><head><meta charset='utf-8'>");
        html.Append("<meta name='viewport' content='width=device-width, initial-scale=1'>");
        html.Append("<title>").Append(E(title)).Append("</title></head>");
        html.Append("<body style='margin:0;padding:0;background-color:#f4f6f9;font-family:Segoe UI,Arial,sans-serif;color:#1f2937;'>");
        html.Append("<table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='background-color:#f4f6f9;padding:24px 0;'><tr><td align='center'>");
        html.Append("<table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='max-width:520px;'><tr>");
        html.Append("<td style='background-color:#ffffff;border-radius:12px;padding:32px;border:1px solid #e5e7eb;'>");
        html.Append("<p style='margin:0 0 20px;font-size:14px;font-weight:700;color:#f26b21;'>AD Gestion Hub</p>");
        html.Append("<h1 style='margin:0 0 16px;font-size:22px;line-height:1.3;color:#111827;'>").Append(E(title)).Append("</h1>");
        html.Append("<p style='margin:0 0 24px;font-size:15px;line-height:1.6;color:#4b5563;'>").Append(E(text)).Append("</p>");
        html.Append("<p style='margin:0 0 24px;'><a href='").Append(E(url)).Append("' style='display:inline-block;background-color:#f26b21;color:#ffffff;text-decoration:none;font-weight:600;padding:12px 22px;border-radius:8px;'>");
        html.Append(E(button)).Append("</a></p>");
        html.Append("<p style='margin:0;font-size:12px;line-height:1.6;color:#6b7280;'>").Append(E(note)).Append("</p>");
        html.Append("</td></tr></table></td></tr></table></body></html>");
        return html.ToString();
    }

    private static string CodePage(string title, string code, string text, string note)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang='fr'><head><meta charset='utf-8'>");
        html.Append("<meta name='viewport' content='width=device-width, initial-scale=1'>");
        html.Append("<title>").Append(E(title)).Append("</title></head>");
        html.Append("<body style='margin:0;padding:0;background-color:#f4f6f9;font-family:Segoe UI,Arial,sans-serif;color:#1f2937;'>");
        html.Append("<table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='background-color:#f4f6f9;padding:24px 0;'><tr><td align='center'>");
        html.Append("<table role='presentation' width='100%' cellspacing='0' cellpadding='0' style='max-width:520px;'><tr>");
        html.Append("<td style='background-color:#ffffff;border-radius:12px;padding:32px;border:1px solid #e5e7eb;'>");
        html.Append("<p style='margin:0 0 20px;font-size:14px;font-weight:700;color:#f26b21;'>AD Gestion Hub</p>");
        html.Append("<h1 style='margin:0 0 16px;font-size:22px;line-height:1.3;color:#111827;'>").Append(E(title)).Append("</h1>");
        html.Append("<p style='margin:0 0 20px;font-size:15px;line-height:1.6;color:#4b5563;'>").Append(E(text)).Append("</p>");
        html.Append("<div style='margin:0 0 24px;padding:18px;text-align:center;background-color:#f9fafb;border:1px dashed #d1d5db;border-radius:8px;'>");
        html.Append("<span style='font-size:30px;font-weight:700;letter-spacing:6px;color:#111827;'>").Append(E(code)).Append("</span></div>");
        html.Append("<p style='margin:0;font-size:12px;line-height:1.6;color:#6b7280;'>").Append(E(note)).Append("</p>");
        html.Append("</td></tr></table></td></tr></table></body></html>");
        return html.ToString();
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value) ?? string.Empty;
}
