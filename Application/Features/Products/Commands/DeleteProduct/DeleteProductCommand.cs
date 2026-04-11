namespace Application.Features.Products.Commands.DeleteProduct;


/// <summary>
/// Soft-deletes a product and all its cascade dependencies in one transaction:
///   • Product (IsDeleted = true)
///   • All ProductImage records (IsDeleted = true)
///   • InventoryRecord (IsDeleted = true)
///   • Any active Offers referencing this product (Status → Inactive)
///
/// IDOR guard: ProductId is scoped to the authenticated retailer's JWT identity.
/// </summary>
public sealed record DeleteProductCommand(Guid ProductId)
    : IRequest<Result>;