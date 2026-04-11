

namespace Application.Features.Inventory.DTOs;

/// <summary>
/// Read model for a single stock adjustment audit entry.
/// Returned as part of InventoryDetailDto.
/// </summary>
public sealed record StockAdjustmentDto(
    Guid Id,
    string AdjustmentType,
    int OldQuantity,
    int NewQuantity,
    string? Reason,
    Guid AdjustedById,
    DateTime AdjustedAt);