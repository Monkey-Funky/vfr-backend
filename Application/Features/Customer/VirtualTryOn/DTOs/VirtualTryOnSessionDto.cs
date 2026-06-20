namespace Application.Features.Customer.VirtualTryOn.DTOs;

public sealed record VirtualTryOnSessionDto(
    Guid Id,
    Guid CustomerId,
    Guid ProductId,
    Guid RetailerId,
    Guid? AvatarId,
    string SessionType,
    string Status,
    string? RecommendedSize,
    decimal? ConfidenceScore,
    // 2D try-on result image URL.
    string? ResultImageUrl,
    // 3D try-on result model URL.
    string? ResultModelUrl,
    int? DurationSeconds,
    DateTime CreatedAt
);
