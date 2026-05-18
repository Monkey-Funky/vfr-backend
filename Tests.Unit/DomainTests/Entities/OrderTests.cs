using Domain.Enums.Orders;

namespace Tests.Unit.DomainTests.Entities;

/// <summary>
/// Unit tests for <see cref="Order"/> aggregate root.
/// Validates factory method, state machine, and total amount calculation.
/// </summary>
public sealed class OrderTests
{
    private static readonly Guid ValidRetailerId = Guid.NewGuid();
    private static readonly Guid ValidCustomerId = Guid.NewGuid();
    private const string ValidCustomerName = "John Doe";

    // ── Factory Method: Create ───────────────────────────────────────────────

    [Fact]
    public void Create_ValidInput_CalculatesTotalAmountCorrectly()
    {
        // Arrange
        var items = new List<(Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity)>
        {
            (Guid.NewGuid(), "Product 1", 100m, 2), // 200
            (Guid.NewGuid(), "Product 2", 50m, 3),  // 150
        };

        // Act
        var order = Order.Create(ValidRetailerId, ValidCustomerId, ValidCustomerName, items);

        // Assert
        order.Should().NotBeNull();
        order.TotalAmount.Should().Be(350m);
        order.Items.Should().HaveCount(2);
        order.Status.Should().Be(OrderStatus.NotProcessed);
    }

    [Fact]
    public void Create_EmptyItems_ThrowsArgumentException()
    {
        // Arrange
        var items = new List<(Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity)>();

        // Act
        var act = () => Order.Create(ValidRetailerId, ValidCustomerId, ValidCustomerName, items);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyRetailerId_ThrowsArgumentException()
    {
        // Arrange
        var items = new List<(Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity)>
        {
            (Guid.NewGuid(), "Product 1", 100m, 1)
        };

        // Act
        var act = () => Order.Create(Guid.Empty, ValidCustomerId, ValidCustomerName, items);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("retailerId");
    }

    // ── UpdateStatus (State Machine) ─────────────────────────────────────────

    [Fact]
    public void UpdateStatus_NotProcessedToProcessing_Succeeds()
    {
        // Arrange
        var order = CreateValidOrder();

        // Act
        order.UpdateStatus(OrderStatus.Processing);

        // Assert
        order.Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public void UpdateStatus_ProcessingToShipped_Succeeds()
    {
        // Arrange
        var order = CreateValidOrder();
        order.UpdateStatus(OrderStatus.Processing);

        // Act
        order.UpdateStatus(OrderStatus.Shipped);

        // Assert
        order.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public void UpdateStatus_ShippedToDelivered_Succeeds()
    {
        // Arrange
        var order = CreateValidOrder();
        order.UpdateStatus(OrderStatus.Processing);
        order.UpdateStatus(OrderStatus.Shipped);

        // Act
        order.UpdateStatus(OrderStatus.Delivered);

        // Assert
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void UpdateStatus_NotProcessedToCancelled_Succeeds()
    {
        // Arrange
        var order = CreateValidOrder();

        // Act
        order.UpdateStatus(OrderStatus.Cancelled);

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void UpdateStatus_DeliveredToAnything_ThrowsBusinessRuleException()
    {
        // Arrange
        var order = CreateValidOrder();
        order.UpdateStatus(OrderStatus.Processing);
        order.UpdateStatus(OrderStatus.Shipped);
        order.UpdateStatus(OrderStatus.Delivered);

        // Act
        var act = () => order.UpdateStatus(OrderStatus.Processing);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("ORDER_INVALID_TRANSITION");
    }

    [Fact]
    public void UpdateStatus_CancelledToAnything_ThrowsBusinessRuleException()
    {
        // Arrange
        var order = CreateValidOrder();
        order.UpdateStatus(OrderStatus.Cancelled);

        // Act
        var act = () => order.UpdateStatus(OrderStatus.Processing);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("ORDER_INVALID_TRANSITION");
    }

    [Fact]
    public void UpdateStatus_InvalidStatusString_ThrowsBusinessRuleException()
    {
        // Arrange
        var order = CreateValidOrder();

        // Act
        var act = () => order.UpdateStatus("InvalidStatus");

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("ORDER_INVALID_STATUS");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Order CreateValidOrder()
    {
        var items = new List<(Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity)>
        {
            (Guid.NewGuid(), "Product 1", 100m, 1)
        };

        return Order.Create(ValidRetailerId, ValidCustomerId, ValidCustomerName, items);
    }
}
