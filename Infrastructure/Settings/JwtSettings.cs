namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "JwtSettings" configuration section.
/// Populated via IOptions{JwtSettings} through DI.
///
/// IMPORTANT: PrivateKeyPem and PublicKeyPem are NOT in appsettings.json.
/// They are loaded directly in Program.cs from user-secrets / Azure Key Vault.
/// StepTokenSecret is likewise a secret — never commit it in plain config.
/// </summary>
public sealed class JwtSettings
{
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int AccessTokenExpiryMinutes { get; init; } = 15;
    public int RefreshTokenExpiryDays { get; init; } = 7;
    public int RememberMeRefreshTokenExpiryDays { get; init; } = 30;

    /// <summary>
    /// HS256 secret exclusively for step tokens.
    /// MUST be sourced from user-secrets / Key Vault — never appsettings.json.
    /// </summary>
    public string? StepTokenSecret { get; init; }
}