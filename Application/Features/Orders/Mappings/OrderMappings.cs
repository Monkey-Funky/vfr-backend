using Application.Features.Orders.DTOs;
using Domain.Entities.Orders;

namespace Application.Features.Orders.Mappings;

/// <summary>
/// Manual mapping extension methods for Order and OrderItem entities.
/// No AutoMapper — all mappings are explicit static extension methods.
/// </summary>
public static class OrderMappings
{
    /// <summary>Maps an Order entity (with Items loaded) to an OrderDto.</summary>
    public static OrderDto ToDto(this Order order)
    {
        return new OrderDto(
            OrderId: order.Id,
            RetailerId: order.RetailerId,
            CustomerId: order.CustomerId,
            CustomerName: order.CustomerName,
            OrderDate: order.OrderDate,
            TotalAmount: order.TotalAmount,
            Currency: order.Currency,
            Status: order.Status,
            ItemCount: order.Items.Count,
            CreatedAt: order.CreatedAt,
            UpdatedAt: order.UpdatedAt,
            Items: order.Items.Select(i => i.ToDto()).ToList().AsReadOnly());
    }

    /// <summary>Maps an OrderItem entity to an OrderItemDto.</summary>
    public static OrderItemDto ToDto(this OrderItem item)
    {
        return new OrderItemDto(
            OrderItemId: item.Id,
            ProductId: item.ProductId,
            ProductName: item.ProductName,
            UnitPrice: item.UnitPrice,
            Quantity: item.Quantity,
            Total: item.Total);
    }

    /// <summary>Maps an Order entity to a lightweight OrderCsvRowDto for CSV export.</summary>
    public static OrderCsvRowDto ToCsvRow(this Order order)
    {
        return new OrderCsvRowDto(
            OrderId: order.Id,
            CustomerName: order.CustomerName,
            OrderDate: order.OrderDate,
            TotalAmount: order.TotalAmount,
            Currency: order.Currency,
            Status: order.Status,
            ItemCount: order.Items.Count,
            CreatedAt: order.CreatedAt);
    }

    /// <summary>Converts an OrderCsvRowDto to a CSV line string.</summary>
    public static string ToCsvLine(this OrderCsvRowDto row)
    {
        return string.Join(",",
            row.OrderId,
            EscapeCsvField(row.CustomerName),
            row.OrderDate.ToString("yyyy-MM-dd HH:mm:ss"),
            row.TotalAmount.ToString("F2"),
            row.Currency,
            row.Status,
            row.ItemCount,
            row.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }
}