namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "CatVTON" configuration section.
/// Controls the CatVTON virtual try-on provider running on Hugging Face Spaces.
/// This provider is completely free — no API key or credit card required.
/// </summary>
public sealed class CatVtonSettings
{
    /// <summary>When false, CatVTON try-on requests are rejected with a 422.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Provider name, surfaced in <c>TryOn2DResult.Provider</c>.</summary>
    public string Provider { get; init; } = "CatVTON";

    /// <summary>
    /// Base URL of the Hugging Face Space hosting CatVTON.
    /// The official Space is <c>https://zhengchong-catvton.hf.space</c>.
    /// </summary>
    public string SpaceUrl { get; init; } = "https://zhengchong-catvton.hf.space";

    /// <summary>
    /// Gradio API endpoint path for the try-on function.
    /// The official CatVTON Space exposes <c>/submit_function</c>.
    /// </summary>
    public string ApiEndpoint { get; init; } = "/gradio_api/call/submit_function";

    /// <summary>
    /// Garment type: "upper" (tops), "lower" (bottoms), "overall" (full outfits/dresses).
    /// Defaults to "overall" to handle any product type generically.
    /// </summary>
    public string ClothType { get; init; } = "overall";

    /// <summary>Number of diffusion inference steps (10–100). Higher = better quality but slower.</summary>
    public int NumInferenceSteps { get; init; } = 50;

    /// <summary>Classifier-free guidance scale (0.0–7.5). Controls adherence to the garment.</summary>
    public double GuidanceScale { get; init; } = 2.5;

    /// <summary>Random seed for reproducibility. -1 for random.</summary>
    public int Seed { get; init; } = 42;

    /// <summary>
    /// Max seconds to wait for the CatVTON job to complete (including HF Space cold-start).
    /// Set higher than fal.ai because free Spaces can take 30–60s to wake up.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 300;

    /// <summary>Interval between SSE poll attempts in milliseconds.</summary>
    public int PollIntervalMs { get; init; } = 2000;
}
