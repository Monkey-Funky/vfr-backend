namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "Email" configuration section.
/// Host, Port, SenderName, SenderEmail are safe in appsettings.json.
/// Username and Password MUST be stored in user-secrets / Key Vault.
/// </summary>
public sealed class EmailSettings
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string SenderName { get; init; } = "Virtual Fitting Room";
    public string SenderEmail { get; init; } = string.Empty;

    /// <summary>SMTP username — sourced from secrets.</summary>
    public string? Username { get; init; }

    /// <summary>SMTP password — sourced from secrets. Never log.</summary>
    public string? Password { get; init; }
}
