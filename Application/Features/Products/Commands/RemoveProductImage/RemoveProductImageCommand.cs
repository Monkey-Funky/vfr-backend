namespace Application.Features.Products.Commands.RemoveProductImage;


/// <summary>
/// Removes a product image: deletes from S3 and soft-deletes the DB record.
/// IDOR guard: ProductId is scoped to the authenticated retailer.
/// </summary>
public sealed record RemoveProductImageCommand(
    Guid ProductId,
    Guid ImageId
) : IRequest<Result>;
