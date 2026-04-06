using Application.Features.Inventory.DTOs;

namespace Application.Features.Inventory.Mappings;

/// <summary>
/// Pure extension methods — no AutoMapper, no reflection.
/// Called by query handlers to project domain entities to DTOs.
/// </summary>
public static class InventoryMappings
{
    /// <summary>Maps an InventoryRecord to the full read-model DTO.</summary>
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

    /// <summary>
    /// Converts an InventoryCsvRowDto to a properly escaped CSV line.
    /// All text fields are passed through <see cref="EscapeCsvField"/> to
    /// handle embedded commas, double-quotes, and newlines.
    /// </summary>
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