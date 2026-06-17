namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "FalAi" configuration section.
/// API Key MUST come from environment variables in production, never appsettings.
/// </summary>
public sealed class FalAiSettings
{
    public string ApiKey { get; init; } = string.Empty;

    // ── SAM 3D APIs (purpose-built for humans + objects + scenes) ──────────
    /// <summary>Hyper3D Rodin — photorealistic textured 3D avatar (PBR skin, face, clothing).</summary>
    public string BodyApiId { get; init; } = "fal-ai/hyper3d/rodin";
    /// <summary>SAM 3D Objects — clothing/item reconstruction ($0.02, 5-10s).</summary>
    public string ObjectsApiId { get; init; } = "fal-ai/sam-3/3d-objects";
    /// <summary>SAM 3D Align — scene composition ($0.02, 5-10s).</summary>
    public string AlignApiId { get; init; } = "fal-ai/sam-3/3d-align";

    // ── Queue Configuration ───────────────────────────────────────────────
    public string QueueBaseUrl { get; init; } = "https://queue.fal.run";
    /// <summary>Rodin needs 30-60s minimum. 120s handles high-res inputs safely.</summary>
    public int MaxPollSeconds { get; init; } = 120;
    /// <summary>Wait 8s before first poll — Rodin min processing time.</summary>
    public int InitialPollDelayMs { get; init; } = 8000;
    /// <summary>Poll every 2s after first check.</summary>
    public int PollIntervalMs { get; init; } = 2000;
}
