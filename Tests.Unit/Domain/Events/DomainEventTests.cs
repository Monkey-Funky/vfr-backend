using Domain.Events;
using Domain.Entities.Orders;

namespace Tests.Unit.Domain.Events;

/// <summary>
/// Unit tests for domain events to ensure property integrity.
/// </summary>
public sealed class DomainEventTests
{
    [Fact]
    public void OrderStatusChangedEvent_Properties_SetCorrectly()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var retailerId = Guid.NewGuid();
        var items = new List<OrderItem>();

        // Act
        var @event = new OrderStatusChangedEvent(orderId, retailerId, "Old", "New", items);

        // Assert
        @event.OrderId.Should().Be(orderId);
        @event.RetailerId.Should().Be(retailerId);
        @event.PreviousStatus.Should().Be("Old");
        @event.NewStatus.Should().Be("New");
        @event.Items.Should().BeSameAs(items);
    }

    [Fact]
    public void LowStockWarningEvent_Properties_SetCorrectly()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var retailerId = Guid.NewGuid();

        // Act
        var @event = new LowStockWarningEvent(
            RetailerId: retailerId,
            ProductId: productId,
            ProductName: "Product A",
            CurrentStock: 5,
            LowStockThreshold: 10);

        // Assert
        @event.RetailerId.Should().Be(retailerId);
        @event.ProductId.Should().Be(productId);
        @event.ProductName.Should().Be("Product A");
        @event.CurrentStock.Should().Be(5);
        @event.LowStockThreshold.Should().Be(10);
    }
}
