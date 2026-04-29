using Application.Features.Customer.Catalog.DTOs;

namespace Application.Features.Customer.Catalog.Queries.BrowseProducts;

public sealed record BrowseProductsQuery(
    Guid? RetailerId = null,
    Guid? CategoryId = null,
    Guid? SubCategoryId = null,
    string? SearchTerm = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string[]? Categories = null,
    string[]? Colors = null,
    string[]? Sizes = null,
    string[]? FabricMaterials = null,
    string[]? BodyShapes = null,
    string[]? FabricPatterns = null,
    string[]? Brands = null,
    string? SortBy = null,
    string? SortOrder = null,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<PagedResult<ProductCardDto>>;
