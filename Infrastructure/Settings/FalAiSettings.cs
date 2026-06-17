namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "FalAi" configuration section.
/// API Key MUST come from environment variables in production, never appsettings.
/// </summary>
public sealed class FalAiSettings
{
    public string ApiKey { get; init; } = string.Empty;

    // ── SAM 3D APIs (purpose-built for humans + objects + scenes) ──────────
    /// <summary>SAM 3D Body — accurate human body reconstruction ($0.02, 5-10s). Output is compatible with sam-3/3d-align.</summary>
    public string BodyApiId { get; init; } = "fal-ai/sam-3/3d-body";
    /// <summary>SAM 3D Objects — clothing/item reconstruction ($0.02, 5-10s).</summary>
    public string ObjectsApiId { get; init; } = "fal-ai/sam-3/3d-objects";
    /// <summary>SAM 3D Align — scene composition ($0.02, 5-10s).</summary>
    public string AlignApiId { get; init; } = "fal-ai/sam-3/3d-align";

    // ── Queue Configuration ───────────────────────────────────────────────
    public string QueueBaseUrl { get; init; } = "https://queue.fal.run";
    /// <summary>SAM 3D Body completes in 5-10s. 120s handles edge cases safely.</summary>
    public int MaxPollSeconds { get; init; } = 120;
    /// <summary>Wait 4s before first poll — SAM 3D typical processing time.</summary>
    public int InitialPollDelayMs { get; init; } = 4000;
    /// <summary>Poll every 1.5s after first check.</summary>
    public int PollIntervalMs { get; init; } = 1500;
}
