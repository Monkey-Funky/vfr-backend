
namespace Application.Features.Orders.DTOs;

/// <summary>
/// Minimal row used by ExportOrdersCsvQueryHandler — avoids loading full navigation items
/// when only summary columns are needed for a CSV export.
/// </summary>
public sealed record OrderCsvRowDto(
    Guid OrderId,
    string CustomerName,
    DateTime OrderDate,
    decimal TotalAmount,
    string Currency,
    string Status,
    int ItemCount,
    DateTime CreatedAt);