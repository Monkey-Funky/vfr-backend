using Domain.Entities.Orders;
using Domain.Enums.Orders;
using Domain.Exceptions;
using FluentAssertions;


namespace Tests.Unit.Domain;

public sealed class OrderStateMachineTests
{
    private static Order BuildOrderInStatus(string targetStatus)
    {
        var items = new List<(Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity)>
        {
            (Guid.NewGuid(), "Product A", 99.99m, 1)
        };

        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), "Test Customer", items);

        if (targetStatus == OrderStatus.NotProcessed)
            return order;

        order.UpdateStatus(OrderStatus.Processing);

        if (targetStatus == OrderStatus.Processing)
            return order;

        if (targetStatus == OrderStatus.Cancelled)
        {
            order.UpdateStatus(OrderStatus.Cancelled);
            return order;
        }

        order.UpdateStatus(OrderStatus.Shipped);

        if (targetStatus == OrderStatus.Shipped)
            return order;

        order.UpdateStatus(OrderStatus.Delivered);
        return order;
    }

    [Theory]
    [InlineData(OrderStatus.NotProcessed, OrderStatus.Processing)]
    [InlineData(OrderStatus.NotProcessed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Processing, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    public void UpdateStatus_ValidTransition_UpdatesStatusAndThrowsNothing(
        string fromStatus, string toStatus)
    {
        // Arrange
        var order = BuildOrderInStatus(fromStatus);

        // Act
        var act = () => order.UpdateStatus(toStatus);

        // Assert
        act.Should().NotThrow();
        order.Status.Should().Be(toStatus);
    }

    [Theory]
    [InlineData(OrderStatus.Delivered, OrderStatus.NotProcessed)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Processing)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.NotProcessed)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Processing)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Shipped, OrderStatus.NotProcessed)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Processing, OrderStatus.NotProcessed)]
    [InlineData(OrderStatus.Processing, OrderStatus.Delivered)]
    [InlineData(OrderStatus.NotProcessed, OrderStatus.Shipped)]
    [InlineData(OrderStatus.NotProcessed, OrderStatus.Delivered)]
    public void UpdateStatus_InvalidTransition_ThrowsBusinessRuleException(
        string fromStatus, string toStatus)
    {
        // Arrange
        var order = BuildOrderInStatus(fromStatus);

        // Act
        var act = () => order.UpdateStatus(toStatus);

        // Assert
        var thrownException = act.Should()
            .Throw<BusinessRuleException>()
            .WithMessage($"*{fromStatus}*")
            .Which;

        thrownException.Message.Should().Contain(toStatus);
    }
}