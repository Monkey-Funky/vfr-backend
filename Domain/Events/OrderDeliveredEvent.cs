
namespace Domain.Events;

/// <summary>
/// Raised inside <see cref="Order.TransitionTo"/> when an Order transitions to
/// the <c>Delivered</c> status.
///
/// Handlers (dispatched in sequence, same DB transaction):
///   1. CommissionDeductionHandler  — calculates and records the platform commission.
///   2. InventoryDecrementHandler   — decrements stock and increments sold quantity
///                                    for each OrderItem.
///
/// OccurredAt is set at the moment of transition — it is NOT the handler execution time.
/// </summary>
public sealed record OrderDeliveredEvent(
    Guid RetailerId,
    Guid OrderId,
    decimal TotalAmount,
    IReadOnlyList<OrderItemSnapshot> Items,
    DateTime OccurredAt
) : IDomainEvent;

/// <summary>
/// An immutable value object capturing the data needed from each OrderItem
/// to perform inventory decrements without an extra DB round-trip in the handler.
/// </summary>
public sealed record OrderItemSnapshot(
    Guid? ProductId,
    string ProductName,
    int Quantity
);
