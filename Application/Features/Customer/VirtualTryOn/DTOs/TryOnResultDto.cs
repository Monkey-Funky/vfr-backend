using Domain.Enums.Customer;

namespace Application.Features.Customer.VirtualTryOn.DTOs;

public sealed record TryOnResultDto(
    SessionStatus Status,
    string? ResultImageUrl,
    string? RecommendedSize,
    decimal? ConfidenceScore,
    int? DurationSeconds,
    // Tells the frontend how to render the result. Image2D → ResultImageUrl; Model3D → ResultModelUrl.
    TryOnResultType? ResultType = TryOnResultType.Model3D,
    // Populated for 3D sessions; null for 2D.
    string? ResultModelUrl = null,
    // True when the result was served from AiGenerationCache without calling fal.ai.
    bool IsCached = false,
    // The persisted VirtualTryOnSession.Id so the frontend can reference the session by server ID.
    Guid? SessionId = null
);
