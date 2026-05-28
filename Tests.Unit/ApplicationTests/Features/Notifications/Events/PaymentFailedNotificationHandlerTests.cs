using Application.Features.Notifications.DTOs;
using Application.Features.Notifications.Events;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;
using Domain.Events;
using Microsoft.Extensions.Logging;
using Tests.Unit.Helpers;

namespace Tests.Unit.ApplicationTests.Features.Notifications.Events;

public sealed class PaymentFailedNotificationHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<Notification>> _repositoryMock = new();
    private readonly Mock<INotificationHub> _notificationHubMock = new();
    private readonly Mock<ILogger<PaymentFailedNotificationHandler>> _loggerMock = new();
    private readonly PaymentFailedNotificationHandler _sut;

    public PaymentFailedNotificationHandlerTests()
    {
        _unitOfWorkMock
            .Setup(u => u.Repository<Notification>())
            .Returns(_repositoryMock.Object);

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification n, CancellationToken _) => n);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _notificationHubMock
            .Setup(h => h.SendNotificationAsync(It.IsAny<string>(), It.IsAny<NotificationDto>()))
            .Returns(Task.CompletedTask);

        _sut = new PaymentFailedNotificationHandler(
            _unitOfWorkMock.Object,
            _notificationHubMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_PaymentFailed_SendsEmailNotification()
    {
        var @event = BuildEvent();

        await _sut.Handle(@event, default);

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PaymentFailed_SendsInAppNotification()
    {
        var @event = BuildEvent();

        await _sut.Handle(@event, default);

        _notificationHubMock.Verify(
            h => h.SendNotificationAsync(
                @event.RetailerId.ToString(),
                It.Is<NotificationDto>(dto => dto.Type == Notification.NotificationType.PaymentFailed)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NotificationContainsPaymentDetails()
    {
        const string failureReason = "Insufficient funds";
        var @event = BuildEvent(failureReason: failureReason);
        Notification? captured = null;

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => captured = n)
            .ReturnsAsync((Notification n, CancellationToken _) => n);

        await _sut.Handle(@event, default);

        captured.Should().NotBeNull();
        captured!.Body.Should().Contain(failureReason);
        captured.Type.Should().Be(Notification.NotificationType.PaymentFailed);
        captured.RetailerId.Should().Be(@event.RetailerId);
        captured.Title.Should().Be("Payment Failed");
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_LogsErrorAndDoesNotRethrow()
    {
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var act = () => _sut.Handle(BuildEvent(), default);

        await act.Should().NotThrowAsync();
        _loggerMock.VerifyLog(LogLevel.Error, "PaymentFailedNotificationHandler failed", Times.Once());
    }

    [Fact]
    public async Task Handle_WhenHubThrowsSynchronously_LogsErrorAndDoesNotRethrow()
    {
        _notificationHubMock
            .Setup(h => h.SendNotificationAsync(It.IsAny<string>(), It.IsAny<NotificationDto>()))
            .Throws(new InvalidOperationException("Hub connection lost"));

        var act = () => _sut.Handle(BuildEvent(), default);

        await act.Should().NotThrowAsync();
        _loggerMock.VerifyLog(LogLevel.Error, "PaymentFailedNotificationHandler failed", Times.Once());
    }

    [Fact]
    public async Task Handle_NotificationIsAssignedToCorrectRetailer()
    {
        var retailerId = Guid.NewGuid();
        var @event = BuildEvent(retailerId: retailerId);
        Notification? captured = null;

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => captured = n)
            .ReturnsAsync((Notification n, CancellationToken _) => n);

        await _sut.Handle(@event, default);

        captured!.RetailerId.Should().Be(retailerId);
    }

    private static PaymentFailedEvent BuildEvent(
        Guid? retailerId = null,
        string failureReason = "Card declined") =>
        new(
            RetailerId: retailerId ?? Guid.NewGuid(),
            FailureReason: failureReason,
            OccurredAt: DateTime.UtcNow);
}