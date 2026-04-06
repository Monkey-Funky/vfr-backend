
using Application.Features.Categories.DTOs;

namespace Application.Features.Categories.Queries.GetSubCategories;

/// <summary>
/// Returns all sub-categories belonging to the specified parent Category.
/// Throws <see cref="NotFoundException"/> (404) if the parent category does not
/// exist or does not belong to the authenticated retailer.
/// </summary>
public sealed record GetSubCategoriesQuery(Guid ParentCategoryId)
    : IRequest<IReadOnlyList<SubCategoryDto>>;