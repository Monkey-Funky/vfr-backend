using Application.Interfaces.Persistence;
using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;
using Infrastructure.Services.Subscription;
using Moq.EntityFrameworkCore;
using Application.Interfaces.Services;

namespace Tests.Unit.Infrastructure.Services;

public sealed class SubscriptionServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly SubscriptionService _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();

    public SubscriptionServiceTests()
    {
        _sut = new SubscriptionService(_contextMock.Object);
    }

    [Fact]
    public async Task GetCurrentPlanAsync_ActiveSubscription_ReturnsPlanInfo()
    {
        // Arrange
        var plan = SubscriptionPlan.Create("Pro", "Standard", "Monthly", 49.99m, "USD", 0.05m, 100, 1000, "24/7");
        var sub = Subscription.Create(RetailerId, plan.Id, SubscriptionStatus.Active, DateTime.UtcNow);

        _contextMock.Setup(x => x.Subscriptions).ReturnsDbSet(new List<Subscription> { sub });
        _contextMock.Setup(x => x.SubscriptionPlans).ReturnsDbSet(new List<SubscriptionPlan> { plan });

        // Act
        var result = await _sut.GetCurrentPlanAsync(RetailerId);

        // Assert
        result.Should().NotBeNull();
        result!.PlanName.Should().Be("Pro");
        result.MaxActiveProducts.Should().Be(100);
    }

    [Fact]
    public async Task GetCurrentPlanAsync_NoActiveSubscription_ReturnsNull()
    {
        // Arrange
        var plan = SubscriptionPlan.Create("Pro", "Standard", "Monthly", 49.99m, "USD", 0.05m, 100, 1000, "24/7");
        var sub = Subscription.Create(RetailerId, plan.Id, SubscriptionStatus.Cancelled, DateTime.UtcNow);

        _contextMock.Setup(x => x.Subscriptions).ReturnsDbSet(new List<Subscription> { sub });
        _contextMock.Setup(x => x.SubscriptionPlans).ReturnsDbSet(new List<SubscriptionPlan> { plan });

        // Act
        var result = await _sut.GetCurrentPlanAsync(RetailerId);

        // Assert
        result.Should().BeNull();
    }
}
