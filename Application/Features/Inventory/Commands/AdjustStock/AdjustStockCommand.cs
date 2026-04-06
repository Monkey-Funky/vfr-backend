namespace Application.Features.Inventory.Commands.AdjustStock;

/// <summary>
/// Sets the stock of an inventory record to an absolute value.
///
/// SECURITY CONTRACT:
///   RetailerId MUST NOT appear here — it is always resolved from the JWT inside the handler.
///   InventoryRecordId is validated for retailer ownership (IDOR guard) in the handler.
///
/// REASON REQUIREMENT:
///   Reason is REQUIRED when Type is ManualIncrease or ManualDecrease.
///   Reason is OPTIONAL for OrderSale and ReturnRestock (automated flows supply their own context).
///   Validated by <see cref="AdjustStockCommandValidator"/>.
/// </summary>
public sealed record AdjustStockCommand(
    Guid InventoryRecordId,
    int NewQuantity,
    string Type,
    string? Reason
) : IRequest<Result<bool>>;