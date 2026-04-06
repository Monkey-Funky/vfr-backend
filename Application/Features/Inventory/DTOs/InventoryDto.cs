
namespace Application.Features.Inventory.DTOs;

/// <summary>
/// Read model returned by GetInventoryQuery and GetInventoryByProductIdQuery.
/// All monetary / audit data is kept as-is; no computation at mapping time.
/// </summary>
public sealed record InventoryDto(
    Guid Id,
    Guid RetailerId,
    Guid ProductId,
    string ProductName,
    int CurrentStock,
    int SoldQuantity,
    int LowStockThreshold,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);