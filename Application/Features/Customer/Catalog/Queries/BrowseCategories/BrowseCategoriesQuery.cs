namespace Application.Features.Customer.Catalog.Queries.BrowseCategories;

public sealed record BrowseCategoriesQuery(Guid? RetailerId = null) : IRequest<PagedResult<CategoryBrowseDto>>;

public sealed record CategoryBrowseDto(
    Guid Id,
    string Name,
    string? Description,
    int ProductCount
);
