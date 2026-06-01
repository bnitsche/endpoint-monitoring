namespace EndpointMonitoring.Core.Notifications;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;

    /// <summary>
    /// False = STARTTLS (port 587 typical).
    /// True = Implicit TLS/SSL on connect (port 465 typical).
    /// </summary>
    public bool UseSsl { get; init; }

    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "Endpoint Monitoring";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}
