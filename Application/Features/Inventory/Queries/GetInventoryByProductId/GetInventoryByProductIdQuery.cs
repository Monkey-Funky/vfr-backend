using Application.Features.Inventory.DTOs;

namespace Application.Features.Inventory.Queries.GetInventoryByProductId;

/// <summary>
/// Returns the single inventory record associated with the given product.
/// Throws <see cref="NotFoundException"/> (→ 404) when the record does not exist
/// or does not belong to the authenticated retailer.
/// RetailerId is resolved from the JWT — IDOR protection.
/// </summary>
public sealed record GetInventoryByProductIdQuery(Guid ProductId)
    : IRequest<InventoryDto>;
