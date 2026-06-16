namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "FalAi" configuration section.
/// API Key MUST come from environment variables in production, never appsettings.
/// </summary>
public sealed class FalAiSettings
{
    public string ApiKey { get; init; } = string.Empty;

    // ── SAM 3D APIs (purpose-built for humans + objects + scenes) ──────────
    /// <summary>SAM 3D Body — human body reconstruction ($0.02, 5-10s).</summary>
    public string BodyApiId { get; init; } = "fal-ai/sam-3/3d-body";
    /// <summary>SAM 3D Objects — clothing/item reconstruction ($0.02, 5-10s).</summary>
    public string ObjectsApiId { get; init; } = "fal-ai/sam-3/3d-objects";
    /// <summary>SAM 3D Align — scene composition ($0.02, 5-10s).</summary>
    public string AlignApiId { get; init; } = "fal-ai/sam-3/3d-align";

    // ── Queue Configuration ───────────────────────────────────────────────
    public string QueueBaseUrl { get; init; } = "https://queue.fal.run";
    /// <summary>SAM 3D is fast (5-30s). 120s timeout handles high-res inputs.</summary>
    public int MaxPollSeconds { get; init; } = 120;
    public int PollIntervalMs { get; init; } = 2000;
}
