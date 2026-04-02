
namespace Infrastructure.Services;

/// <summary>
/// Strongly-typed binding for the "Google" configuration section.
/// ClientId is the OAuth2 Web Client ID from Google Cloud Console.
/// It is NOT a secret and may be in appsettings.json.
/// </summary>
public sealed class GoogleSettings
{
    public string ClientId { get; init; } = string.Empty;
}