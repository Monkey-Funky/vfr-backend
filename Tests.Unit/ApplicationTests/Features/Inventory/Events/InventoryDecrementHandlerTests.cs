using Application.Features.Orders.Events;
using Application.Interfaces.Persistence;
using Domain.Enums.Orders;
using Domain.Events;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.ApplicationTests.Features.Orders.Events;

public sealed class InventoryDecrementHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<InventoryDecrementHandler>> _loggerMock = new();
    private readonly Mock<IRepository<InventoryRecord>> _inventoryRepoMock = new();
    private readonly Mock<IRepository<StockAdjustment>> _adjustmentRepoMock = new();
    private readonly InventoryDecrementHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public InventoryDecrementHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Repository<InventoryRecord>()).Returns(_inventoryRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Repository<StockAdjustment>()).Returns(_adjustmentRepoMock.Object);

        _inventoryRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<InventoryRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _adjustmentRepoMock
            .Setup(r => r.AddAsync(It.IsAny<StockAdjustment>(), It.IsAny<CancellationToken>()))
            .Returns((StockAdjustment s, CancellationToken _) => Task.FromResult(s));

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _sut = new InventoryDecrementHandler(_unitOfWorkMock.Object, _loggerMock.Object);
    }

    private static InventoryRecord BuildInventory(int stock = 50, Guid? productId = null)
    {
        return InventoryRecord.Create(
            retailerId: RetailerId,
            productId: productId ?? ProductId,
            productName: "Test Product",
            initialQuantity: stock,
            lowStockThreshold: 10);
    }

    private static OrderStatusChangedEvent BuildShippedEvent(IReadOnlyList<OrderItem> items)
    {
        return new OrderStatusChangedEvent(
            OrderId: OrderId,
            RetailerId: RetailerId,
            PreviousStatus: OrderStatus.Processing,
            NewStatus: OrderStatus.Shipped,
            Items: items);
    }

    [Fact]
    public async Task Handle_OrderPlacedEvent_DecrementsStockForEachItem()
    {
        var inventory = BuildInventory(stock: 50);
        _inventoryRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InventoryRecord, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var item = OrderItem.Create(OrderId, ProductId, "Product", 100m, 5);
        var @event = BuildShippedEvent([item]);

        var previousStock = inventory.CurrentStock;
        await _sut.Handle(@event, CancellationToken.None);

        inventory.CurrentStock.Should().Be(previousStock - 5);
        _inventoryRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<InventoryRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OrderPlacedEvent_PersistsStockChanges()
    {
        var inventory = BuildInventory(stock: 50);
        _inventoryRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InventoryRecord, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var item = OrderItem.Create(OrderId, ProductId, "Product", 100m, 3);
        var @event = BuildShippedEvent([item]);

        await _sut.Handle(@event, CancellationToken.None);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OutOfStock_ThrowsBusinessRuleException()
    {
        var inventory = BuildInventory(stock: 3);
        _inventoryRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InventoryRecord, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var item = OrderItem.Create(OrderId, ProductId, "Product", 100m, 10);
        var @event = BuildShippedEvent([item]);

        var act = () => _sut.Handle(@event, CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<BusinessRuleException>();
        assertion.Which.Code.Should().Be("INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task Handle_MultipleItems_AllDecremented()
    {
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();
        var productId3 = Guid.NewGuid();

        var inventory = BuildInventory(stock: 50);
        _inventoryRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InventoryRecord, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var items = new List<OrderItem>
        {
            OrderItem.Create(OrderId, productId1, "Product A", 100m, 5),
            OrderItem.Create(OrderId, productId2, "Product B", 200m, 3),
            OrderItem.Create(OrderId, productId3, "Product C", 150m, 2)
        };
        var @event = BuildShippedEvent(items);

        await _sut.Handle(@event, CancellationToken.None);

        _inventoryRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<InventoryRecord>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        _adjustmentRepoMock.Verify(
            r => r.AddAsync(It.IsAny<StockAdjustment>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }
}