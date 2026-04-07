using Application.Interfaces.Services;
namespace Application.Features.Dashboard.Events;

/// <summary>
/// Listens for OrderDeliveredEvent and invalidates the entire dashboard cache
/// for the retailer that owns the delivered order.
///
/// Cache key pattern invalidated: "dashboard:{retailerId}:*"
/// This covers KPIs, revenue, profit, sessions, return analytics, and conversion rate.
/// The real-time activity feed has no cache to invalidate.
///
/// NOTE: OrderDeliveredEvent implements IDomainEvent (not MediatR's INotification directly).
/// If IDomainEvent : INotification, this handler is auto-discovered by MediatR.
/// If IDomainEvent is a custom marker, register this as INotificationHandler<OrderDeliveredEvent>
/// only if OrderDeliveredEvent also implements INotification through IDomainEvent.
/// </summary>
public sealed class OrderDeliveredEventHandler
    : INotificationHandler<OrderDeliveredEvent>
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<OrderDeliveredEventHandler> _logger;

    public OrderDeliveredEventHandler(
        ICacheService cacheService,
        ILogger<OrderDeliveredEventHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task Handle(
        OrderDeliveredEvent notification,
        CancellationToken cancellationToken)
    {
        string pattern = $"dashboard:{notification.RetailerId}:*";

        await _cacheService.RemoveByPatternAsync(pattern, cancellationToken);

        _logger.LogInformation(
            "Dashboard cache invalidated for RetailerId {RetailerId} " +
            "after OrderDelivered (OrderId={OrderId}, Amount={Amount}). Pattern: {Pattern}",
            notification.RetailerId,
            notification.OrderId,
            notification.TotalAmount,
            pattern);
    }
}