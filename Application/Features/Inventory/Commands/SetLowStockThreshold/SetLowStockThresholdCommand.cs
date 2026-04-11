namespace Application.Features.Inventory.Commands.SetLowStockThreshold;

/// <summary>
/// Sets the low stock threshold for a specific inventory record.
///
/// SECURITY: RetailerId is resolved from the JWT inside the handler — never from the request.
/// IDOR guard: InventoryRecordId is validated against the authenticated retailer.
///
/// THRESHOLD RULE: Must be >= 0 and <= 10 000 (validated by FluentValidation).
/// </summary>
public sealed record SetLowStockThresholdCommand(
    Guid InventoryRecordId,
    int NewThreshold) : IRequest<Result<bool>>;