using Domain.Enums.Customer;

namespace Application.Features.Customer.VirtualTryOn.DTOs;

public sealed record TryOnResultDto(
    SessionStatus Status,
    string? ResultImageUrl,
    string? RecommendedSize,
    decimal? ConfidenceScore,
    int? DurationSeconds
);
