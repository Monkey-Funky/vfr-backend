
namespace Application.Features.Categories.Queries.GetCategoryById;


/// <summary>
/// Returns a single Category by ID, including its sub-category count.
/// Throws <see cref="NotFoundException"/> if the category does not exist
/// or does not belong to the authenticated retailer.
/// </summary>
public sealed record GetCategoryByIdQuery(Guid CategoryId) : IRequest<CategoryDto>;