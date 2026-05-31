using Application.Features.Settings.Commands.UpdateNotificationPreferences;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Settings.Commands;

public sealed class UpdateNotificationPreferencesCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ILogger<UpdateNotificationPreferencesCommandHandler>> _loggerMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<IRepository<NotificationPreference>> _prefRepoMock = new();
    private readonly UpdateNotificationPreferencesCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();

    public UpdateNotificationPreferencesCommandHandlerTests()
    {
        _currentUserServiceMock.Setup(x => x.RetailerId).Returns(RetailerId);

        _uowMock.Setup(x => x.Repository<NotificationPreference>()).Returns(_prefRepoMock.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _prefRepoMock.Setup(x => x.UpdateAsync(
                It.IsAny<NotificationPreference>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _prefRepoMock.Setup(x => x.AddAsync(
                It.IsAny<NotificationPreference>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference p, CancellationToken _) => p);

        _sut = new UpdateNotificationPreferencesCommandHandler(
            _uowMock.Object,
            _currentUserServiceMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    private NotificationPreference CreateDefaultPreference()
    {
        return NotificationPreference.CreateDefault(RetailerId);
    }

    private void SetupPreferenceFound(NotificationPreference? preference)
    {
        _prefRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<NotificationPreference, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preference);
    }

    [Fact]
    public async Task Handle_RetailerNotFound_ThrowsNotFoundException()
    {
        _currentUserServiceMock.Setup(x => x.RetailerId).Returns((Guid?)null);

        var command = new UpdateNotificationPreferencesCommand(false, false, false, false, false);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidPreferences_UpdatesAllFlags()
    {
        var preference = CreateDefaultPreference();
        SetupPreferenceFound(preference);

        var command = new UpdateNotificationPreferencesCommand(
            LowStockAlerts: false,
            OrderStatusAlerts: false,
            SubscriptionAlerts: false,
            EmailNotifications: false,
            InAppNotifications: false);

        await _sut.Handle(command, CancellationToken.None);

        preference.LowStockAlerts.Should().BeFalse();
        preference.OrderStatusAlerts.Should().BeFalse();
        preference.SubscriptionAlerts.Should().BeFalse();
        preference.EmailNotifications.Should().BeFalse();
        preference.InAppNotifications.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsChanges()
    {
        var preference = CreateDefaultPreference();
        SetupPreferenceFound(preference);

        var command = new UpdateNotificationPreferencesCommand(true, null, null, null, null);
        await _sut.Handle(command, CancellationToken.None);

        _prefRepoMock.Verify(x => x.UpdateAsync(preference, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsUpdatedPreferences()
    {
        SetupPreferenceFound(CreateDefaultPreference());

        var command = new UpdateNotificationPreferencesCommand(false, true, null, false, null);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        result.Message.Should().NotBeNullOrEmpty();
    }
}