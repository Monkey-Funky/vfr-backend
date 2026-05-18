using Domain.Enums.Product;

namespace Tests.Unit.DomainTests.Entities;

/// <summary>
/// Unit tests for <see cref="InventoryRecord"/> aggregate root.
/// Validates stock adjustments, status derivation, and threshold management.
/// </summary>
public sealed class InventoryRecordTests
{
    private static readonly Guid ValidRetailerId = Guid.NewGuid();
    private static readonly Guid ValidProductId = Guid.NewGuid();
    private static readonly Guid ValidUserId = Guid.NewGuid();
    private const string ValidProductName = "Test Product";

    // ── Factory Method: Create ───────────────────────────────────────────────

    [Fact]
    public void Create_ValidInput_ReturnsInventoryRecord()
    {
        // Act
        var record = InventoryRecord.Create(
            ValidRetailerId,
            ValidProductId,
            ValidProductName,
            initialQuantity: 50,
            lowStockThreshold: 15);

        // Assert
        record.Should().NotBeNull();
        record.RetailerId.Should().Be(ValidRetailerId);
        record.ProductId.Should().Be(ValidProductId);
        record.ProductName.Should().Be(ValidProductName);
        record.CurrentStock.Should().Be(50);
        record.LowStockThreshold.Should().Be(15);
        record.SoldQuantity.Should().Be(0);
        record.Status.Should().Be(InventoryStatus.InStock);
    }

    [Fact]
    public void Create_NegativeQuantity_ThrowsBusinessRuleException()
    {
        // Act
        var act = () => InventoryRecord.Create(ValidRetailerId, ValidProductId, ValidProductName, -1);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("INVALID_QUANTITY");
    }

    // ── AdjustStock (Delta) ──────────────────────────────────────────────────

    [Fact]
    public void AdjustStock_PositiveDelta_IncreasesStock()
    {
        // Arrange
        var record = InventoryRecord.Create(ValidRetailerId, ValidProductId, ValidProductName, 10);

        // Act
        record.AdjustStock(5, AdjustmentType.ManualIncrease, ValidUserId);

        // Assert
        record.CurrentStock.Should().Be(15);
        record.Status.Should().Be(InventoryStatus.InStock);
    }

    [Fact]
    public void AdjustStock_NegativeDelta_DecreasesStock()
    {
        // Arrange
        var record = InventoryRecord.Create(ValidRetailerId, ValidProductId, ValidProductName, 10);

        // Act
        record.AdjustStock(-3, AdjustmentType.ManualDecrease, ValidUserId);

        // Assert
        record.CurrentStock.Should().Be(7);
    }

    [Fact]
    public void AdjustStock_DecreaseBelowZero_ThrowsBusinessRuleException()
    {
        // Arrange
        var record = InventoryRecord.Create(ValidRetailerId, ValidProductId, ValidProductName, 5);

        // Act
        var act = () => record.AdjustStock(-6, AdjustmentType.ManualDecrease, ValidUserId);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("INSUFFICIENT_STOCK");
    }

    [Fact]
    public void AdjustStock_OrderSale_IncreasesSoldQuantity()
    {
        // Arrange
        var record = InventoryRecord.Create(ValidRetailerId, ValidProductId, ValidProductName, 10);

        // Act
        record.AdjustStock(-2, AdjustmentType.OrderSale, ValidUserId);

        // Assert
        record.CurrentStock.Should().Be(8);
        record.SoldQuantity.Should().Be(2);
    }

    // ── AdjustStock (Absolute) ───────────────────────────────────────────────

    [Fact]
    public void AdjustStock_AbsoluteNewQuantity_SetsStock()
    {
        // Arrange
        var record = InventoryRecord.Create(ValidRetailerId, ValidProductId, ValidProductName, 10);

        // Act
        record.AdjustStock(25, AdjustmentType.ManualIncrease, "Restock", ValidUserId);

        // Assert
        record.CurrentStock.Should().Be(25);
    }

    // ── Status Derivation ────────────────────────────────────────────────────

    [Theory]
    [InlineData(20, 10, InventoryStatus.InStock)]
    [InlineData(10, 10, InventoryStatus.LowStock)]
    [InlineData(5, 10, InventoryStatus.LowStock)]
    [InlineData(0, 10, InventoryStatus.OutOfStock)]
    public void Status_ShouldMatchStockAndThreshold(int stock, int threshold, string expectedStatus)
    {
        // Arrange
        var record = InventoryRecord.Create(ValidRetailerId, ValidProductId, ValidProductName, stock, threshold);

        // Assert
        record.Status.Should().Be(expectedStatus);
    }

    // ── Threshold Management ─────────────────────────────────────────────────

    [Fact]
    public void SetLowStockThreshold_ValidValue_UpdatesThresholdAndStatus()
    {
        // Arrange
        var record = InventoryRecord.Create(ValidRetailerId, ValidProductId, ValidProductName, 12, 10);
        record.Status.Should().Be(InventoryStatus.InStock);

        // Act
        record.SetLowStockThreshold(15);

        // Assert
        record.LowStockThreshold.Should().Be(15);
        record.Status.Should().Be(InventoryStatus.LowStock);
    }
}
