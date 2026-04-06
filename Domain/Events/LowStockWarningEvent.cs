
using MediatR;

namespace Domain.Events;

/// <summary>
/// Raised by <see cref="InventoryDecrementHandler"/> when an InventoryRecord's
/// CurrentStock drops to or below its LowStockThreshold after an order delivery.
///
/// Handler:
///   LowStockWarningHandler — creates a LowStock Notification for the retailer.
/// </summary>
public sealed record LowStockWarningEvent(
    Guid RetailerId,
    Guid ProductId,
    string ProductName,
    int CurrentStock,
    int LowStockThreshold) : INotification;