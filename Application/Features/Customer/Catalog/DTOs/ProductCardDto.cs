namespace Application.Features.Customer.Catalog.DTOs;

public sealed record ProductCardDto(
    Guid Id,
    string Name,
    string? BrandName,
    decimal? Price,
    decimal? DiscountedPrice,
    string? PrimaryImageUrl,
    string[]? AvailableColors,
    bool IsFavorite
);
