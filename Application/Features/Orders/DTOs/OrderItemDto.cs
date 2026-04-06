
namespace Application.Features.Orders.DTOs;

/// <summary>
/// Flat projection of a single OrderItem (line item).
/// </summary>
public sealed record OrderItemDto(
    Guid OrderItemId,
    Guid? ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Total);
