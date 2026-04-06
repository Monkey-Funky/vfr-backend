
namespace Application.Features.Inventory.DTOs;

/// <summary>
/// Lightweight projection used when streaming inventory CSV rows.
/// Keeps only the fields that appear in the export file.
/// </summary>
public sealed record InventoryCsvRowDto(
    Guid InventoryRecordId,
    Guid ProductId,
    string ProductName,
    int CurrentStock,
    int SoldQuantity,
    int LowStockThreshold,
    string Status,
    DateTime CreatedAt);
