using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace AdGestionHub.Services;

/// <summary>
/// Envoi réel des e-mails Identity via SMTP. Ne fait plus de no-op silencieux :
/// une configuration absente remonte une erreur explicite dans les journaux.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.From))
        {
            throw new InvalidOperationException(
                "L'envoi d'e-mails n'est pas configuré. Renseignez la section Smtp " +
                "(Host, Port, UserName, Password, From) ; voir docs/email-brevo.md.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.From, _options.FromName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(email);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Timeout = Math.Max(1, _options.TimeoutSeconds) * 1000
        };
        if (!string.IsNullOrWhiteSpace(_options.UserName))
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);

        try
        {
            await client.SendMailAsync(message);
            _logger.LogInformation("E-mail « {Subject} » envoyé.", subject);
        }
        catch (Exception error)
        {
            _logger.LogError(error, "Échec de l'envoi de l'e-mail « {Subject} ».", subject);
            throw;
        }
    }
}
