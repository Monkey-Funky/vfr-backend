namespace Application.Features.Customer.Avatar.DTOs;

public sealed record SizeRecommendationDto(
    Guid ProductId,
    string RecommendedSize,
    decimal ConfidenceScore,
    string Justification);
