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
    string? ResultImageUrl,
    int? DurationSeconds,
    DateTime CreatedAt
);
