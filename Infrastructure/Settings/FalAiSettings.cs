namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "FalAi" configuration section.
/// API Key MUST come from environment variables in production, never appsettings.
/// </summary>
public sealed class FalAiSettings
{
    public string ApiKey { get; init; } = string.Empty;
    public string BodyApiId { get; init; } = "fal-ai/sam-3/3d-body";
    public string ObjectsApiId { get; init; } = "fal-ai/sam-3/3d-objects";
    public string AlignApiId { get; init; } = "fal-ai/sam-3/3d-align";
    public string QueueBaseUrl { get; init; } = "https://queue.fal.run";
    public int MaxPollSeconds { get; init; } = 60;
    public int PollIntervalMs { get; init; } = 2000;
}
