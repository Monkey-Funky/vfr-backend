namespace Application.Features.Products.Commands.ToggleProductStatus;


/// <summary>
/// Toggles a product's status between Active and Inactive.
/// If the product is in Draft, it transitions to Active.
/// IDOR guard: ProductId is scoped to the authenticated retailer.
/// </summary>
public sealed record ToggleProductStatusCommand(Guid ProductId)
    : IRequest<Result<string>>;