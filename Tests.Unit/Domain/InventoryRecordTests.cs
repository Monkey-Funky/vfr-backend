using Domain.Entities.Retailer;
using Domain.Enums.Product;
using Domain.Exceptions;
using FluentAssertions;


namespace Tests.Unit.Domain;

public sealed class InventoryRecordTests
{
    private const int DefaultThreshold = 10;
    private const int AboveThresholdStock = 50;

    private static readonly Guid AdjustedById = Guid.NewGuid();

    private static InventoryRecord BuildRecord(int initialStock, int threshold = DefaultThreshold)
        => InventoryRecord.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Product",
            initialStock,
            threshold);

    [Fact]
    public void AdjustStock_PositiveDelta_IncreasesCurrentStock()
    {
        // Arrange
        var record = BuildRecord(initialStock: 20);
        const int delta = 15;

        // Act
        record.AdjustStock(delta, AdjustmentType.ManualIncrease, AdjustedById);

        // Assert
        record.CurrentStock.Should().Be(35);
    }

    [Fact]
    public void AdjustStock_NegativeDeltaWithinBounds_DecreasesCurrentStock()
    {
        // Arrange
        var record = BuildRecord(initialStock: 30);
        const int delta = -8;

        // Act
        record.AdjustStock(delta, AdjustmentType.ManualDecrease, AdjustedById);

        // Assert
        record.CurrentStock.Should().Be(22);
    }

    [Fact]
    public void AdjustStock_DeltaThatWouldMakeStockNegative_ThrowsBusinessRuleException()
    {
        // Arrange
        var record = BuildRecord(initialStock: 5);
        const int delta = -10;

        // Act
        var act = () => record.AdjustStock(delta, AdjustmentType.ManualDecrease, AdjustedById);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*stock below zero*");
    }

    [Fact]
    public void AdjustStock_StockLandsExactlyAtLowStockThreshold_StatusIsLowStock()
    {
        // Arrange
        var record = BuildRecord(initialStock: AboveThresholdStock, threshold: DefaultThreshold);
        int delta = -(AboveThresholdStock - DefaultThreshold);

        // Act
        record.AdjustStock(delta, AdjustmentType.ManualDecrease, AdjustedById);

        // Assert
        record.CurrentStock.Should().Be(DefaultThreshold);
        record.Status.Should().Be(InventoryStatus.LowStock);
    }

    [Fact]
    public void AdjustStock_StockLandsBelowLowStockThreshold_StatusIsLowStock()
    {
        // Arrange
        var record = BuildRecord(initialStock: AboveThresholdStock, threshold: DefaultThreshold);
        int delta = -(AboveThresholdStock - DefaultThreshold + 5);

        // Act
        record.AdjustStock(delta, AdjustmentType.ManualDecrease, AdjustedById);

        // Assert
        record.CurrentStock.Should().BeLessThan(DefaultThreshold);
        record.Status.Should().BeOneOf(InventoryStatus.LowStock, InventoryStatus.OutOfStock);
    }

    [Fact]
    public void AdjustStock_StockLandsAboveLowStockThreshold_StatusIsInStock()
    {
        // Arrange
        var record = BuildRecord(initialStock: DefaultThreshold, threshold: DefaultThreshold);
        const int delta = 25;

        // Act
        record.AdjustStock(delta, AdjustmentType.ManualIncrease, AdjustedById);

        // Assert
        record.CurrentStock.Should().BeGreaterThan(DefaultThreshold);
        record.Status.Should().Be(InventoryStatus.InStock);
    }
}