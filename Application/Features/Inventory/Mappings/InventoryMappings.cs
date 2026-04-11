using Application.Features.Inventory.DTOs;

namespace Application.Features.Inventory.Mappings;


/// <summary>
/// Pure extension methods — no AutoMapper, no reflection.
/// Called by query handlers to project domain entities to DTOs.
/// StockAdjustment.AdjustedAt returns BaseEntity.CreatedAt (computed property).
/// </summary>
public static class InventoryMappings
{
    /// <summary>Maps an InventoryRecord to the list read-model DTO (no adjustments).</summary>
    public static InventoryDto ToDto(this InventoryRecord record) =>
        new(
            Id: record.Id,
            RetailerId: record.RetailerId,
            ProductId: record.ProductId,
            ProductName: record.ProductName,
            CurrentStock: record.CurrentStock,
            SoldQuantity: record.SoldQuantity,
            LowStockThreshold: record.LowStockThreshold,
            Status: record.Status,
            CreatedAt: record.CreatedAt,
            UpdatedAt: record.UpdatedAt);

    /// <summary>
    /// Maps an InventoryRecord to the detail DTO including StockAdjustments.
    /// The record must have been loaded with Include(r => r.StockAdjustments).
    /// </summary>
    public static InventoryDetailDto ToDetailDto(this InventoryRecord record) =>
        new(
            Id: record.Id,
            RetailerId: record.RetailerId,
            ProductId: record.ProductId,
            ProductName: record.ProductName,
            CurrentStock: record.CurrentStock,
            SoldQuantity: record.SoldQuantity,
            LowStockThreshold: record.LowStockThreshold,
            Status: record.Status,
            CreatedAt: record.CreatedAt,
            UpdatedAt: record.UpdatedAt,
            StockAdjustments: record.StockAdjustments
                .OrderByDescending(a => a.AdjustedAt)
                .Select(a => a.ToDto())
                .ToList());

    /// <summary>Maps a StockAdjustment to its read-model DTO.</summary>
    public static StockAdjustmentDto ToDto(this StockAdjustment adjustment) =>
        new(
            Id: adjustment.Id,
            AdjustmentType: adjustment.AdjustmentType,
            OldQuantity: adjustment.OldQuantity,
            NewQuantity: adjustment.NewQuantity,
            Reason: adjustment.Reason,
            AdjustedById: adjustment.AdjustedById,
            AdjustedAt: adjustment.AdjustedAt);

    /// <summary>Maps an InventoryRecord to the CSV row projection.</summary>
    public static InventoryCsvRowDto ToCsvRow(this InventoryRecord record) =>
        new(
            InventoryRecordId: record.Id,
            ProductId: record.ProductId,
            ProductName: record.ProductName,
            CurrentStock: record.CurrentStock,
            SoldQuantity: record.SoldQuantity,
            LowStockThreshold: record.LowStockThreshold,
            Status: record.Status,
            CreatedAt: record.CreatedAt);

    /// <summary>Converts an InventoryCsvRowDto to a properly escaped CSV line.</summary>
    public static string ToCsvLine(this InventoryCsvRowDto row) =>
        string.Join(",",
            row.InventoryRecordId,
            row.ProductId,
            EscapeCsvField(row.ProductName),
            row.CurrentStock,
            row.SoldQuantity,
            row.LowStockThreshold,
            row.Status,
            row.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }
}