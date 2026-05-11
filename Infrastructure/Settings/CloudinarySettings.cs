namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "Cloudinary" configuration section.
/// API Key and Secret MUST come from environment variables, never appsettings.
/// </summary>
public sealed class CloudinarySettings
{
    public string CloudName { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ApiSecret { get; init; } = string.Empty;
}