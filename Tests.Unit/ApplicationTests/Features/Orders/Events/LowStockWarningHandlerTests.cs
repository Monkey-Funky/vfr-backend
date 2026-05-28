using Application.Features.Orders.Events;
using Application.Interfaces.Persistence;
using Domain.Entities.Notifications;
using Domain.Enums.Orders;
using Domain.Events;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.ApplicationTests.Features.Orders.Events;

public sealed class LowStockWarningHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ILogger<LowStockWarningHandler>> _loggerMock = new();
    private readonly Mock<IRepository<InventoryRecord>> _inventoryRepoMock = new();
    private readonly Mock<IRepository<Notification>> _notificationRepoMock = new();
    private readonly LowStockWarningHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public LowStockWarningHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Repository<InventoryRecord>()).Returns(_inventoryRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Repository<Notification>()).Returns(_notificationRepoMock.Object);

        _notificationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns((Notification n, CancellationToken _) => Task.FromResult(n));

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        SetupEmptyNotificationsDbSet();

        _sut = new LowStockWarningHandler(_unitOfWorkMock.Object, _contextMock.Object, _loggerMock.Object);
    }

    private void SetupEmptyNotificationsDbSet()
    {
        var empty = new List<Notification>();
        var mockDbSet = empty.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.Notifications).Returns(mockDbSet.Object);
    }

    private static InventoryRecord BuildInventory(int currentStock, int lowStockThreshold)
    {
        var inventory = InventoryRecord.Create(
            retailerId: RetailerId,
            productId: ProductId,
            productName: "Test Product",
            initialQuantity: currentStock,
            lowStockThreshold: lowStockThreshold);
        return inventory;
    }

    private static OrderStatusChangedEvent BuildShippedEvent(Guid? productId = null)
    {
        var item = OrderItem.Create(OrderId, productId ?? ProductId, "Test Product", 100m, 1);
        return new OrderStatusChangedEvent(
            OrderId: OrderId,
            RetailerId: RetailerId,
            PreviousStatus: OrderStatus.Processing,
            NewStatus: OrderStatus.Shipped,
            Items: [item]);
    }

    [Fact]
    public async Task Handle_StockBelowThreshold_SendsNotification()
    {
        var inventory = BuildInventory(currentStock: 3, lowStockThreshold: 10);
        _inventoryRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InventoryRecord, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var @event = BuildShippedEvent();

        await _sut.Handle(@event, CancellationToken.None);

        _notificationRepoMock.Verify(
            r => r.AddAsync(It.Is<Notification>(n => n.Type == Notification.NotificationType.LowStock), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StockAboveThreshold_DoesNotSendNotification()
    {
        var inventory = BuildInventory(currentStock: 25, lowStockThreshold: 10);
        _inventoryRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InventoryRecord, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var @event = BuildShippedEvent();

        await _sut.Handle(@event, CancellationToken.None);

        _notificationRepoMock.Verify(
            r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ThresholdIsZero_SkipsNotification()
    {
        var inventory = BuildInventory(currentStock: 5, lowStockThreshold: 0);
        _inventoryRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InventoryRecord, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        var @event = BuildShippedEvent();

        await _sut.Handle(@event, CancellationToken.None);

        _notificationRepoMock.Verify(
            r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NotificationIsSentToCorrectRetailer()
    {
        var inventory = BuildInventory(currentStock: 2, lowStockThreshold: 10);
        _inventoryRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<InventoryRecord, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);

        Notification? capturedNotification = null;
        _notificationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => capturedNotification = n)
            .Returns((Notification n, CancellationToken _) => Task.FromResult(n));

        var @event = BuildShippedEvent();

        await _sut.Handle(@event, CancellationToken.None);

        capturedNotification.Should().NotBeNull();
        capturedNotification!.RetailerId.Should().Be(RetailerId);
        capturedNotification.Type.Should().Be(Notification.NotificationType.LowStock);
        capturedNotification.ResourceId.Should().Be(ProductId);
    }
}