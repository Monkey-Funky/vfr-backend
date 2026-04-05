namespace Application.Features.Products.Commands.DeleteProduct;


/// <summary>
/// Soft-deletes a product AND its associated InventoryRecord in the same transaction.
/// IDOR guard: ProductId is scoped to the authenticated retailer's JWT identity.
/// </summary>
public sealed record DeleteProductCommand(Guid ProductId)
    : IRequest<Result>;
