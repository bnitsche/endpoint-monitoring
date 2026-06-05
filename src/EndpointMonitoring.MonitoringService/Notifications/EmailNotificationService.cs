using EndpointMonitoring.Core.Models;
using EndpointMonitoring.Core.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace EndpointMonitoring.MonitoringService.Notifications;

/// <summary>Sends failure alert emails via SMTP using the configured <see cref="SmtpSettings"/>.</summary>
public sealed class EmailNotificationService(
    SmtpSettings settings,
    ILogger<EmailNotificationService> logger)
{
    /// <summary>Sends an alert email for the given failed endpoint to all subscribed recipients.</summary>
    public async Task SendAlertAsync(
        MonitoredEndpoint endpoint,
        MonitoringResult result,
        IEnumerable<string> recipientEmails,
        string websiteUrl,
        CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured)
        {
            logger.LogDebug("SMTP not configured — skipping alert email for '{Endpoint}'.", endpoint.Name);
            return;
        }

        var recipients = recipientEmails.ToList();
        if (recipients.Count == 0)
        {
            logger.LogDebug("No notification recipients — skipping alert email for '{Endpoint}'.", endpoint.Name);
            return;
        }

        var message = BuildMessage(endpoint, result, recipients, websiteUrl);

        try
        {
            using var client = new SmtpClient();
            var socketOptions = settings.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;

            await client.ConnectAsync(settings.Host, settings.Port, socketOptions, cancellationToken);

            if (!string.IsNullOrEmpty(settings.Username))
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            logger.LogInformation(
                "Alert email sent for endpoint '{Endpoint}' to {Count} recipient(s).",
                endpoint.Name, recipients.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send alert email for endpoint '{Endpoint}'.", endpoint.Name);
        }
    }

    private MimeMessage BuildMessage(
        MonitoredEndpoint endpoint,
        MonitoringResult result,
        List<string> recipients,
        string websiteUrl)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));

        foreach (var email in recipients)
            message.To.Add(MailboxAddress.Parse(email));

        message.Subject = $"Monitoring Alert: {endpoint.Name}";

        var dashboardUrl = websiteUrl.TrimEnd('/');
        var body = $"""
            Endpoint monitoring has detected a failure.

            Endpoint:     {endpoint.Name}
            Provider:     {endpoint.ProviderType}
            Failure time: {result.CheckedAt:u} (UTC)
            Status:       {result.StatusMessage ?? "(no message)"}
            Details:      {result.Details ?? "(none)"}

            View the dashboard: {dashboardUrl}

            ---
            This is an automated message from Endpoint Monitoring.
            To stop receiving these alerts, ask an administrator to disable notifications for your account.
            """;

        message.Body = new TextPart(TextFormat.Plain) { Text = body };
        return message;
    }
}
