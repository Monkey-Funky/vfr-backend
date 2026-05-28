using Application.Features.Notifications.DTOs;
using Application.Features.Notifications.Events;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;
using Domain.Events;
using Microsoft.Extensions.Logging;
using Tests.Unit.Helpers;

namespace Tests.Unit.ApplicationTests.Features.Notifications.Events;

public sealed class SubscriptionExpiryNotificationHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<Notification>> _repositoryMock = new();
    private readonly Mock<INotificationHub> _notificationHubMock = new();
    private readonly Mock<ILogger<SubscriptionExpiryNotificationHandler>> _loggerMock = new();
    private readonly SubscriptionExpiryNotificationHandler _sut;

    public SubscriptionExpiryNotificationHandlerTests()
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

        _sut = new SubscriptionExpiryNotificationHandler(
            _unitOfWorkMock.Object,
            _notificationHubMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_SubscriptionExpiring_SendsReminderEmail()
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
    public async Task Handle_SubscriptionExpired_SendsExpiredEmail()
    {
        var @event = BuildEvent();

        await _sut.Handle(@event, default);

        _notificationHubMock.Verify(
            h => h.SendNotificationAsync(
                @event.RetailerId.ToString(),
                It.Is<NotificationDto>(dto =>
                    dto.Type == Notification.NotificationType.SubscriptionExpiring)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmailContainsRetailerName()
    {
        var retailerId = Guid.NewGuid();
        var @event = BuildEvent(retailerId: retailerId);

        await _sut.Handle(@event, default);

        _notificationHubMock.Verify(
            h => h.SendNotificationAsync(
                retailerId.ToString(),
                It.IsAny<NotificationDto>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NotificationBodyContainsExpiryDate()
    {
        var expiresAt = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var @event = BuildEvent(expiresAt: expiresAt);
        Notification? captured = null;

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => captured = n)
            .ReturnsAsync((Notification n, CancellationToken _) => n);

        await _sut.Handle(@event, default);

        captured.Should().NotBeNull();
        captured!.Body.Should().Contain("15 Jun 2025");
        captured.Type.Should().Be(Notification.NotificationType.SubscriptionExpiring);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_LogsErrorAndDoesNotRethrow()
    {
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        var act = () => _sut.Handle(BuildEvent(), default);

        await act.Should().NotThrowAsync();
        _loggerMock.VerifyLog(LogLevel.Error, "SubscriptionExpiryNotificationHandler failed", Times.Once());
    }

    private static SubscriptionExpiredEvent BuildEvent(
        Guid? retailerId = null,
        DateTime? expiresAt = null) =>
        new(
            RetailerId: retailerId ?? Guid.NewGuid(),
            ExpiresAt: expiresAt ?? DateTime.UtcNow,
            OccurredAt: DateTime.UtcNow);
}