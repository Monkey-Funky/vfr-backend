using Application.Features.Notifications.DTOs;
using Application.Features.Notifications.Events;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;
using Domain.Events;
using Microsoft.Extensions.Logging;

namespace Tests.Unit.ApplicationTests.Features.Notifications.Events;

public sealed class OrderDeliveredNotificationHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<INotificationHub> _notificationHubMock = new();
    private readonly Mock<ILogger<OrderDeliveredNotificationHandler>> _loggerMock = new();
    private readonly Mock<IRepository<Notification>> _notificationRepoMock = new();
    private readonly OrderDeliveredNotificationHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public OrderDeliveredNotificationHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Repository<Notification>()).Returns(_notificationRepoMock.Object);

        _notificationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns((Notification n, CancellationToken _) => Task.FromResult(n));

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _notificationHubMock
            .Setup(h => h.SendNotificationAsync(It.IsAny<string>(), It.IsAny<NotificationDto>()))
            .Returns(Task.CompletedTask);

        _sut = new OrderDeliveredNotificationHandler(
            _unitOfWorkMock.Object,
            _notificationHubMock.Object,
            _loggerMock.Object);
    }

    private static OrderDeliveredEvent BuildOrderDeliveredEvent(Guid? retailerId = null, Guid? orderId = null)
    {
        return new OrderDeliveredEvent(
            RetailerId: retailerId ?? RetailerId,
            OrderId: orderId ?? OrderId,
            TotalAmount: 1000m,
            Items: [new OrderItemSnapshot(Guid.NewGuid(), "Product A", 2)],
            OccurredAt: DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_OrderDelivered_SendsNotificationToCustomer()
    {
        var @event = BuildOrderDeliveredEvent();

        await _sut.Handle(@event, CancellationToken.None);

        _notificationHubMock.Verify(
            h => h.SendNotificationAsync(It.IsAny<string>(), It.IsAny<NotificationDto>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OrderDelivered_SendsNotificationToRetailer()
    {
        var @event = BuildOrderDeliveredEvent();

        await _sut.Handle(@event, CancellationToken.None);

        _notificationHubMock.Verify(
            h => h.SendNotificationAsync(
                It.Is<string>(id => id == RetailerId.ToString()),
                It.IsAny<NotificationDto>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NotificationContainsOrderId()
    {
        var @event = BuildOrderDeliveredEvent();
        var expectedRef = OrderId.ToString()[..8].ToUpperInvariant();

        Notification? capturedNotification = null;
        _notificationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => capturedNotification = n)
            .Returns((Notification n, CancellationToken _) => Task.FromResult(n));

        await _sut.Handle(@event, CancellationToken.None);

        capturedNotification.Should().NotBeNull();
        capturedNotification!.Body.Should().Contain(expectedRef);
        capturedNotification.Type.Should().Be(Notification.NotificationType.OrderStatusChanged);
    }

    [Fact]
    public async Task Handle_NotificationPersisted()
    {
        var @event = BuildOrderDeliveredEvent();

        await _sut.Handle(@event, CancellationToken.None);

        _notificationRepoMock.Verify(
            r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}