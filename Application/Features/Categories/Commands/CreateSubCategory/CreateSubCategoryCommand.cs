
namespace Application.Features.Categories.Commands.CreateSubCategory;


/// <summary>
/// Creates a SubCategory under an existing parent Category.
/// The handler enforces:
///   1. The parent category exists and belongs to the authenticated retailer.
///   2. The name is unique within the parent category (partial index guard).
///   3. Depth limit: the parent must be a <see cref="Category"/>, not a <see cref="SubCategory"/>.
/// </summary>
public sealed record CreateSubCategoryCommand(
    Guid ParentCategoryId,
    string Name,
    string Status
) : IRequest<Result<Guid>>;