using Domain.Enums.Product;
using Domain.Enums.Orders;

namespace Tests.Unit.Domain.Enums;

/// <summary>
/// Unit tests for domain constants and pseudo-enums.
/// Ensures completeness and validity logic.
/// </summary>
public sealed class EnumTests
{
    [Fact]
    public void InventoryStatus_All_ContainsExpectedValues()
    {
        // Assert
        InventoryStatus.All.Should().HaveCount(3);
        InventoryStatus.All.Should().Contain(InventoryStatus.InStock);
        InventoryStatus.All.Should().Contain(InventoryStatus.LowStock);
        InventoryStatus.All.Should().Contain(InventoryStatus.OutOfStock);
    }

    [Theory]
    [InlineData(50, 10, InventoryStatus.InStock)]
    [InlineData(10, 10, InventoryStatus.LowStock)]
    [InlineData(5, 10, InventoryStatus.LowStock)]
    [InlineData(0, 10, InventoryStatus.OutOfStock)]
    [InlineData(-1, 10, InventoryStatus.OutOfStock)]
    public void InventoryStatus_Derive_ReturnsCorrectStatus(int stock, int threshold, string expected)
    {
        // Act
        var result = InventoryStatus.Derive(stock, threshold);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void OrderStatus_All_ContainsExpectedValues()
    {
        // Assert
        OrderStatus.All.Should().HaveCount(5);
        OrderStatus.All.Should().Contain(OrderStatus.NotProcessed);
        OrderStatus.All.Should().Contain(OrderStatus.Processing);
        OrderStatus.All.Should().Contain(OrderStatus.Shipped);
        OrderStatus.All.Should().Contain(OrderStatus.Delivered);
        OrderStatus.All.Should().Contain(OrderStatus.Cancelled);
    }

    [Theory]
    [InlineData(OrderStatus.NotProcessed, true)]
    [InlineData(OrderStatus.Processing, true)]
    [InlineData("Invalid", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OrderStatus_IsValid_ReturnsExpectedResult(string? status, bool expected)
    {
        // Act
        var result = OrderStatus.IsValid(status!);

        // Assert
        result.Should().Be(expected);
    }
}
