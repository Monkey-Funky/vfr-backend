using Application.Features.Notifications.Queries.GetUnreadCount;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;

namespace Tests.Unit.Application.Features.Notifications;

public sealed class GetUnreadCountQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly GetUnreadCountQueryHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OtherRetailerId = Guid.NewGuid();

    public GetUnreadCountQueryHandlerTests()
    {
        _sut = new GetUnreadCountQueryHandler(
            _contextMock.Object,
            _userServiceMock.Object,
            _cacheServiceMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
    }

    private static Notification BuildNotification(
        Guid? retailerId = null,
        bool isRead = false)
    {
        var n = Notification.Create(
            retailerId ?? RetailerId,
            Notification.NotificationType.NewOrder,
            "Title",
            "Body");

        if (isRead) n.MarkAsRead();
        return n;
    }

    private void SetupNotificationsDbSet(List<Notification> notifications)
    {
        var mockSet = notifications.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.Notifications).Returns(mockSet.Object);
    }

    [Fact]
    public async Task Handle_NoUnreadNotifications_ReturnsZero()
    {
        SetupNotificationsDbSet([]);

        var result = await _sut.Handle(new GetUnreadCountQuery(), CancellationToken.None);

        result.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnreadExist_ReturnsCorrectCount()
    {
        var notifications = new List<Notification>
        {
            BuildNotification(isRead: false),
            BuildNotification(isRead: false),
            BuildNotification(isRead: true),
            BuildNotification(isRead: false),
        };
        SetupNotificationsDbSet(notifications);

        var result = await _sut.Handle(new GetUnreadCountQuery(), CancellationToken.None);

        result.Should().Be(3);
    }

    [Fact]
    public async Task Handle_CountIsUserSpecific()
    {
        var notifications = new List<Notification>
        {
            BuildNotification(retailerId: RetailerId, isRead: false),
            BuildNotification(retailerId: RetailerId, isRead: false),
            BuildNotification(retailerId: OtherRetailerId, isRead: false),
            BuildNotification(retailerId: OtherRetailerId, isRead: false),
        };
        SetupNotificationsDbSet(notifications);

        var result = await _sut.Handle(new GetUnreadCountQuery(), CancellationToken.None);

        result.Should().Be(2);
    }

    [Fact]
    public async Task Handle_AllNotificationsRead_ReturnsZero()
    {
        var notifications = new List<Notification>
        {
            BuildNotification(isRead: true),
            BuildNotification(isRead: true),
        };
        SetupNotificationsDbSet(notifications);

        var result = await _sut.Handle(new GetUnreadCountQuery(), CancellationToken.None);

        result.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NullRetailerId_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new GetUnreadCountQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}