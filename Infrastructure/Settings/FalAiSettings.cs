namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "FalAi" configuration section.
/// API Key MUST come from environment variables in production, never appsettings.
/// </summary>
public sealed class FalAiSettings
{
    public string ApiKey { get; init; } = string.Empty;

    // ── Avatar Body APIs ──────────────────────────────────────────────────
    /// <summary>Hyper3D Rodin — 3D avatar generation from image (~$0.04/generation).</summary>
    public string BodyApiId { get; init; } = "fal-ai/hyper3d/rodin";
    /// <summary>SAM 3D Objects — clothing/item reconstruction ($0.02, typically 15-600s depending on garment complexity).</summary>
    public string ObjectsApiId { get; init; } = "fal-ai/sam-3/3d-objects";
    /// <summary>SAM 3D Align — scene composition ($0.02, 5-20s).</summary>
    public string AlignApiId { get; init; } = "fal-ai/sam-3/3d-align";

    // ── Queue Configuration ───────────────────────────────────────────────
    public string QueueBaseUrl { get; init; } = "https://queue.fal.run";

    /// <summary>
    /// Poll budget for sam-3/3d-body and sam-3/3d-align (typically 5-20s).
    /// 300s gives a very wide safety margin for those two fast calls.
    /// </summary>
    public int MaxPollSeconds { get; init; } = 300;

    /// <summary>
    /// Dedicated poll budget for sam-3/3d-objects.
    /// Complex garments (e.g. blazers with intricate geometry) have been observed
    /// taking up to 537s on fal.ai. 700s gives comfortable headroom.
    /// </summary>
    public int ObjectsApiPollSeconds { get; init; } = 700;

    /// <summary>Wait 4s before first poll — SAM 3D typical processing time.</summary>
    public int InitialPollDelayMs { get; init; } = 4000;
    /// <summary>Poll every 1.5s after first check.</summary>
    public int PollIntervalMs { get; init; } = 1500;
}
