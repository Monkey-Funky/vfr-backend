namespace Application.Features.Categories.Queries.GetCategories;


/// <summary>
/// Returns a paginated, optionally status-filtered list of categories for the
/// authenticated retailer. Results are cached in Redis with a 30-minute TTL.
/// </summary>
public sealed record GetCategoriesQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null
) : IRequest<PagedResult<CategoryDto>>;