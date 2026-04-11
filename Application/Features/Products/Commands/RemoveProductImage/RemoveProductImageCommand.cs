namespace Application.Features.Products.Commands.RemoveProductImage;

/// <summary>
/// Removes a product image: soft-deletes the DB record, then best-effort deletes from S3.
/// IDOR guard: ProductId is scoped to the authenticated retailer.
/// </summary>
public sealed record RemoveProductImageCommand(
    Guid ProductId,
    Guid ImageId
) : IRequest<Result>;