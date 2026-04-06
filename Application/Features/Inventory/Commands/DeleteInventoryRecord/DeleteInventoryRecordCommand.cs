namespace Application.Features.Inventory.Commands.DeleteInventoryRecord;

/// <summary>
/// Soft-deletes an inventory record.
///
/// IMPORTANT: Soft-deleting the inventory record does NOT delete or deactivate
/// the parent Product. The product remains fully operational in the catalogue.
/// Only the inventory tracking entry is hidden from all inventory queries.
///
/// RetailerId is ALWAYS resolved from the JWT inside the handler — never from the request.
/// </summary>
public sealed record DeleteInventoryRecordCommand(
    Guid InventoryRecordId
) : IRequest<Result<bool>>;