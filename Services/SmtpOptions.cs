namespace AdGestionHub.Services;

/// <summary>
/// Paramètres SMTP utilisés pour les e-mails transactionnels (confirmation, mot de passe oublié, codes).
/// Les valeurs sensibles doivent provenir de l'environnement ou du coffre de secrets, jamais du dépôt.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Hôte SMTP du fournisseur (Brevo : smtp-relay.brevo.net).</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Port SMTP. Le 587 avec STARTTLS est recommandé pour Brevo.</summary>
    public int Port { get; set; } = 587;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>Adresse expéditrice vérifiée chez le fournisseur.</summary>
    public string From { get; set; } = string.Empty;

    public string FromName { get; set; } = "AD Gestion Hub";

    public bool EnableSsl { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 30;
}
