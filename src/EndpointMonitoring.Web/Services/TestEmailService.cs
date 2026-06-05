using EndpointMonitoring.Core.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace EndpointMonitoring.Web.Services;

/// <summary>Sends a single test email to verify that the SMTP configuration is correct.</summary>
public sealed class TestEmailService(
    SmtpSettings settings,
    ILogger<TestEmailService> logger)
{
    /// <summary>Sends a test email to <paramref name="recipientEmail"/>. Throws if SMTP is not configured.</summary>
    public async Task SendTestEmailAsync(string recipientEmail, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured)
            throw new InvalidOperationException("SMTP is not configured. Set the Smtp section in appsettings.json.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = "Endpoint Monitoring — Test Email";
        message.Body = new TextPart(TextFormat.Plain)
        {
            Text = """
                This is a test notification from Endpoint Monitoring.

                If you received this email, alert notifications are configured correctly for your account.

                ---
                This is an automated message from Endpoint Monitoring.
                """
        };

        using var client = new SmtpClient();
        var socketOptions = settings.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(settings.Host, settings.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrEmpty(settings.Username))
            await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("Test email sent to {Recipient}.", recipientEmail);
    }
}
