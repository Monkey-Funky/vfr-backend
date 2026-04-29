namespace Application.Features.Customer.Catalog.DTOs;

public sealed record ProductDetailDto(
    Guid Id,
    string Name,
    string? BrandName,
    decimal? Price,
    decimal? DiscountedPrice,
    string? Description,
    string? Features,
    string? WashInstructions,
    int ViewsCount,
    decimal? AverageRating,
    int ReviewCount,
    string[]? AvailableSizes,
    string[]? AvailableColors,
    string StockStatus,
    bool IsFavorite,
    ProductAttributesDto Attributes,
    List<ProductImageDto> Images,
    CategoryInfoDto? Category
);
