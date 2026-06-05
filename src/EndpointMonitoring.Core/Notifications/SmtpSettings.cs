namespace EndpointMonitoring.Core.Notifications;

/// <summary>SMTP connection and sender settings, bound from the <c>Smtp</c> configuration section.</summary>
public sealed class SmtpSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Smtp";

    /// <summary>SMTP server hostname or IP.</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>SMTP server port (default 587).</summary>
    public int Port { get; init; } = 587;

    /// <summary>
    /// False = STARTTLS (port 587 typical).
    /// True = Implicit TLS/SSL on connect (port 465 typical).
    /// </summary>
    public bool UseSsl { get; init; }

    /// <summary>SMTP authentication username, or <see langword="null"/> for anonymous relay.</summary>
    public string? Username { get; init; }

    /// <summary>SMTP authentication password.</summary>
    public string? Password { get; init; }

    /// <summary>Envelope <c>From</c> address.</summary>
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>Display name shown alongside <see cref="FromAddress"/>.</summary>
    public string FromName { get; init; } = "Endpoint Monitoring";

    /// <summary>Returns <see langword="true"/> when the minimum required fields are populated.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}
