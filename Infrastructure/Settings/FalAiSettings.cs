namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "FalAi" configuration section.
/// API Key MUST come from environment variables in production, never appsettings.
/// </summary>
public sealed class FalAiSettings
{
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Hyper3D Rodin — production-ready, textured 3D avatar generation.
    /// </summary>
    public string BodyApiId { get; init; } = "fal-ai/hyper3d/rodin";

    public string ObjectsApiId { get; init; } = "fal-ai/sam-3/3d-objects";
    public string AlignApiId { get; init; } = "fal-ai/sam-3/3d-align";
    public string QueueBaseUrl { get; init; } = "https://queue.fal.run";

    /// <summary>Rodin high-quality generation can take up to 2-3 minutes.</summary>
    public int MaxPollSeconds { get; init; } = 180;
    public int PollIntervalMs { get; init; } = 3000;

    // ── Rodin-specific quality knobs ──────────────────────────────────────
    /// <summary>Generation quality: high | medium | low | extra-low.</summary>
    public string AvatarQuality { get; init; } = "high";

    /// <summary>Material type: PBR (realistic) | Shaded.</summary>
    public string AvatarMaterial { get; init; } = "PBR";

    /// <summary>Tier: Regular (production) | Sketch (fast draft).</summary>
    public string AvatarTier { get; init; } = "Regular";

    /// <summary>
    /// Enable HighPack addon for 4K textures + high-poly mesh.
    /// Dramatically improves face and skin detail but costs 3× standard.
    /// </summary>
    public bool EnableHighPack { get; init; } = true;
}
