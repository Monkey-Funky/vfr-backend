using Application.Features.Notifications.DTOs;
using Application.Features.Notifications.Queries.GetNotifications;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;

namespace Tests.Unit.Application.Features.Notifications;

public sealed class GetNotificationsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly GetNotificationsQueryHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OtherRetailerId = Guid.NewGuid();

    public GetNotificationsQueryHandlerTests()
    {
        _sut = new GetNotificationsQueryHandler(
            _contextMock.Object,
            _userServiceMock.Object,
            _cacheMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _cacheMock
            .Setup(x => x.GetAsync<NotificationsPagedResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationsPagedResult?)null);

        _cacheMock
            .Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<NotificationsPagedResult>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Notification BuildNotification(
        Guid? retailerId = null,
        bool isRead = false,
        string? type = null)
    {
        var n = Notification.Create(
            retailerId ?? RetailerId,
            type ?? Notification.NotificationType.LowStock,
            "Test Title",
            "Test Body");

        if (isRead) n.MarkAsRead();
        return n;
    }

    private void SetupNotificationsDbSet(List<Notification> notifications)
    {
        var mockSet = notifications.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.Notifications).Returns(mockSet.Object);
    }

    [Fact]
    public async Task Handle_NoNotifications_ReturnsEmptyPagedResult()
    {
        SetupNotificationsDbSet([]);

        var result = await _sut.Handle(new GetNotificationsQuery(null, 1, 20), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.UnreadCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NotificationsExist_ReturnsPaged()
    {
        var notifications = new List<Notification>
        {
            BuildNotification(),
            BuildNotification(isRead: true),
            BuildNotification(),
        };
        SetupNotificationsDbSet(notifications);

        var result = await _sut.Handle(new GetNotificationsQuery(null, 1, 20), CancellationToken.None);

        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_FilterByUnread_ReturnsOnlyUnread()
    {
        var notifications = new List<Notification>
        {
            BuildNotification(isRead: false),
            BuildNotification(isRead: true),
            BuildNotification(isRead: false),
        };
        SetupNotificationsDbSet(notifications);

        var result = await _sut.Handle(new GetNotificationsQuery(IsRead: false, 1, 20), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(n => n.IsRead.Should().BeFalse());
    }

    [Fact]
    public async Task Handle_UserSeesOnlyOwnNotifications()
    {
        var ownNotification = BuildNotification(retailerId: RetailerId);
        var otherNotification = BuildNotification(retailerId: OtherRetailerId);
        SetupNotificationsDbSet([ownNotification, otherNotification]);

        var result = await _sut.Handle(new GetNotificationsQuery(null, 1, 20), CancellationToken.None);

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NullRetailerId_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new GetNotificationsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_UnreadCountReflectsAllUnreadForRetailer()
    {
        var notifications = new List<Notification>
        {
            BuildNotification(isRead: false),
            BuildNotification(isRead: false),
            BuildNotification(isRead: true),
        };
        SetupNotificationsDbSet(notifications);

        var result = await _sut.Handle(new GetNotificationsQuery(IsRead: true, 1, 20), CancellationToken.None);

        result.UnreadCount.Should().Be(2);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_CacheHit_DoesNotQueryDatabase()
    {
        var cached = new NotificationsPagedResult
        {
            Items = [],
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20,
            UnreadCount = 0
        };

        _cacheMock
            .Setup(x => x.GetAsync<NotificationsPagedResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var result = await _sut.Handle(new GetNotificationsQuery(), CancellationToken.None);

        result.Should().BeSameAs(cached);
        _contextMock.Verify(c => c.Notifications, Times.Never);
    }
}