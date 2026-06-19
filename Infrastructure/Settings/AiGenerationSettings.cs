namespace Infrastructure.Settings;

public sealed class AiGenerationSettings
{
    /// <summary>
    /// Bump this whenever the prompt, provider, model, or preprocessing changes.
    /// All cached results with a different version will be treated as cache misses.
    /// </summary>
    public string PipelineVersion { get; init; } = "2026-06-19-v1";

    /// <summary>Maximum paid (non-cached) avatar generations per customer per day.</summary>
    public int MaxPaidAvatarGenerationsPerDay { get; init; } = 5;

    /// <summary>Maximum paid (non-cached) try-on generations per customer per day.</summary>
    public int MaxPaidTryOnGenerationsPerDay { get; init; } = 20;

    /// <summary>
    /// How many hours a failed cache entry must be old before a retry is allowed.
    /// </summary>
    public int FailedRetryWindowHours { get; init; } = 1;
}
