
namespace Application.Features.Inventory.DTOs;

/// <summary>
/// Detail read model returned by GetInventoryByProductIdQuery.
/// Includes the full StockAdjustment audit trail ordered by AdjustedAt DESC.
/// </summary>
public sealed record InventoryDetailDto(
    Guid Id,
    Guid RetailerId,
    Guid ProductId,
    string ProductName,
    int CurrentStock,
    int SoldQuantity,
    int LowStockThreshold,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<StockAdjustmentDto> StockAdjustments);