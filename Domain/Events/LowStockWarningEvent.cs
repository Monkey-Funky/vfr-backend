
using MediatR;

namespace Domain.Events;

/// <summary>
/// Raised inside AdjustStockCommandHandler immediately after AdjustStock() succeeds
/// and the new CurrentStock is at or below the LowStockThreshold.
///
/// Handlers:
///   • LowStockWarningEventHandler — creates an in-app Notification and/or sends
///     an email, depending on NotificationPreferences.
///
/// PARAMETER NAMES: Use exact names (RetailerId, ProductId, ProductName,
/// CurrentStock, LowStockThreshold) when constructing with named arguments.
/// The parameter is named LowStockThreshold (not Threshold) to match the
/// domain property name and avoid ambiguity.
/// </summary>
public sealed record LowStockWarningEvent(
    Guid RetailerId,
    Guid ProductId,
    string ProductName,
    int CurrentStock,
    int LowStockThreshold) : INotification;