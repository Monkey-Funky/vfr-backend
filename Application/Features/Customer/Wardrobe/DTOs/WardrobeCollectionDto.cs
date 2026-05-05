namespace Application.Features.Customer.Wardrobe.DTOs;

public sealed record WardrobeCollectionDto(
    Guid Id,
    string Name,
    int ItemCount,
    string? CoverImageUrl
);
