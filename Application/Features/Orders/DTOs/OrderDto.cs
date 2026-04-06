
namespace Application.Features.Orders.DTOs;

/// <summary>
/// Flat projection of an Order with its line items.
/// Returned by GetOrdersQuery and GetOrderByIdQuery.
/// </summary>
public sealed record OrderDto(
    Guid OrderId,
    Guid RetailerId,
    Guid CustomerId,
    string CustomerName,
    DateTime OrderDate,
    decimal TotalAmount,
    string Currency,
    string Status,
    int ItemCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<OrderItemDto> Items);