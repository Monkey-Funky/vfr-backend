namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "FalAi" configuration section.
/// API Key MUST come from environment variables in production, never appsettings.
/// </summary>
public sealed class FalAiSettings
{
    public string ApiKey { get; init; } = string.Empty;

    // ── 3D Generation APIs ────────────────────────────────────────────────
    /// <summary>Hyper3D Rodin — production-ready, textured 3D avatar generation.</summary>
    public string BodyApiId { get; init; } = "fal-ai/hyper3d/rodin";
    public string ObjectsApiId { get; init; } = "fal-ai/sam-3/3d-objects";
    public string AlignApiId { get; init; } = "fal-ai/sam-3/3d-align";

    // ── Image Preprocessing APIs ──────────────────────────────────────────
    /// <summary>BiRefNet v2 — background removal with Portrait model for human subjects.</summary>
    public string BackgroundRemovalApiId { get; init; } = "fal-ai/birefnet/v2";
    /// <summary>AuraSR — GAN-based super-resolution image upscaler (4× factor).</summary>
    public string ImageUpscaleApiId { get; init; } = "fal-ai/aura-sr";

    // ── Queue Configuration ───────────────────────────────────────────────
    public string QueueBaseUrl { get; init; } = "https://queue.fal.run";
    /// <summary>Rodin high-quality + preprocessing pipeline can take 3-4 minutes total.</summary>
    public int MaxPollSeconds { get; init; } = 240;
    public int PollIntervalMs { get; init; } = 3000;

    // ── Rodin Quality Knobs ───────────────────────────────────────────────
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

    // ── Preprocessing Toggles ─────────────────────────────────────────────
    /// <summary>Enable image preprocessing (background removal + upscaling) before 3D generation.</summary>
    public bool EnablePreprocessing { get; init; } = true;
}
