namespace Application.Features.Customer.Catalog.DTOs;

public sealed record ProductComparisonDto(
    Guid Id,
    string Name,
    string? BrandName,
    decimal? Price,
    decimal? DiscountedPrice,
    string? PrimaryImageUrl,
    decimal? AverageRating,
    string StockStatus,
    ProductAttributesDto Attributes
);
