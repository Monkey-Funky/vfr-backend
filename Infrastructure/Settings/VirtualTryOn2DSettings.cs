namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "VirtualTryOn2D" configuration section.
/// Controls the 2D (Overlay2D) try-on provider. The fal.ai API key, queue base
/// URL and poll cadence are shared with <see cref="FalAiSettings"/>; only the
/// model id and per-job timeout are specific to 2D.
/// </summary>
public sealed class VirtualTryOn2DSettings
{
    /// <summary>When false, 2D try-on requests are rejected with a 422.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Provider name, surfaced in <c>TryOn2DResult.Provider</c>. Currently only "FalAi".</summary>
    public string Provider { get; init; } = "FalAi";

    /// <summary>fal.ai model/app id for the 2D try-on (FASHN). Defaults to fal-ai/fashn/tryon/v1.6.</summary>
    public string ModelId { get; init; } = "fal-ai/fashn/tryon/v1.6";

    /// <summary>Max seconds to wait for the queued 2D job to complete before timing out.</summary>
    public int TimeoutSeconds { get; init; } = 180;
}
