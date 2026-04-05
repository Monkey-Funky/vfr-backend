namespace Application.Features.Products.Queries.GetProducts;


/// <summary>
/// Returns a paginated list of products for the authenticated retailer.
///
/// Filters:
///   - CategoryId / SubCategoryId   → exact FK match
///   - Status                       → exact match (e.g. "Active")
///   - SearchTerm                   → full-text search via plainto_tsquery
///
/// RULE: Uses AsNoTracking().AsSplitQuery() in the repository implementation.
/// RULE: RetailerId is NEVER accepted as a query parameter — always from JWT.
/// </summary>
public sealed record GetProductsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? CategoryId = null,
    Guid? SubCategoryId = null,
    string? Status = null,
    string? SearchTerm = null
) : IRequest<PagedResult<ProductListDto>>;