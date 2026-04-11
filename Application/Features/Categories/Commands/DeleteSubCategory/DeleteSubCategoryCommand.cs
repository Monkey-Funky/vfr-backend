
namespace Application.Features.Categories.Commands.DeleteSubCategory;

/// <summary>
/// Soft-deletes a SubCategory.
/// Cascade: products.SubCategoryId → null, in the same PostgreSQL transaction.
/// </summary>
public sealed record DeleteSubCategoryCommand(
    Guid ParentCategoryId,
    Guid SubCategoryId
) : IRequest<Result<bool>>;