

namespace Application.Features.Categories.Commands.DeleteCategory;


/// <summary>
/// Soft-deletes a Category and cascades the following operations —
/// all within a single PostgreSQL transaction:
///   1. Offers with this CategoryId → Status set to Inactive.
///   2. Products with this CategoryId → CategoryId set to null.
///   3. All SubCategories of this Category → soft-deleted.
///   4. The Category itself → soft-deleted (is_deleted = true).
///
/// Returns <see cref="Result{T}"/> of bool — the API layer maps this to HTTP 204.
/// </summary>
public sealed record DeleteCategoryCommand(Guid CategoryId) : IRequest<Result<bool>>;