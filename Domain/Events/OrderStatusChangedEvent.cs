using Domain.Entities.Orders;
using MediatR;

namespace Domain.Events;

/// <summary>
/// Published inside the UpdateOrderStatus transaction when an order's status changes.
/// Handled by:
///   • InventoryDecrementHandler  — decrements stock when status → Shipped
///   • LowStockWarningHandler     — sends a low-stock notification if stock drops below threshold
///   • CommissionDeductionHandler — records platform commission when status → Delivered
///
/// TRANSACTION GUARANTEE:
///   All handlers run INSIDE the same ExecuteInTransactionAsync block that saved the
///   status change. If any handler throws, the entire transaction rolls back — including
///   the status change, the inventory decrement, and the notification insert.
///   This guarantees all-or-nothing semantics across all side effects.
/// </summary>
public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    Guid RetailerId,
    string PreviousStatus,
    string NewStatus,
    IReadOnlyList<OrderItem> Items) : INotification;
