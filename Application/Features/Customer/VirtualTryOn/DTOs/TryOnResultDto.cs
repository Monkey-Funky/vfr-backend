using Domain.Enums.Customer;

namespace Application.Features.Customer.VirtualTryOn.DTOs;

public sealed record TryOnResultDto(
    SessionStatus Status,
    string? ResultImageUrl,
    string? RecommendedSize,
    decimal? ConfidenceScore,
    int? DurationSeconds,
    // Tells the frontend how to render ResultImageUrl. Defaults to Model3D so the
    // existing 3D/AR flow is unchanged; the 2D path sets Image2D explicitly.
    TryOnResultType? ResultType = TryOnResultType.Model3D
);
