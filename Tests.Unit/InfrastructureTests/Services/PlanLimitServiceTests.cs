using Application.Interfaces.Persistence;
using Domain.Entities.Retailer;
using Domain.Enums.Subscription;
using Infrastructure.Services.Subscription;
using Microsoft.Extensions.Logging;
using Moq.EntityFrameworkCore;
using global::Domain.Exceptions;
using Domain.Entities.Subscriptions;
using Domain.Enums.Product;

namespace Tests.Unit.InfrastructureTests.Services;

public sealed class PlanLimitServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ILogger<PlanLimitService>> _loggerMock = new();
    private readonly PlanLimitService _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();

    public PlanLimitServiceTests()
    {
        _sut = new PlanLimitService(_contextMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task EnforceAsync_NoSubscription_LogsWarningAndReturns()
    {
        // Arrange
        _contextMock.Setup(x => x.Subscriptions).ReturnsDbSet(new List<Subscription>());

        // Act
        await _sut.EnforceAsync(RetailerId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No subscription found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task EnforceAsync_UnlimitedPlan_DoesNotCountProducts()
    {
        // Arrange
        var plan = SubscriptionPlan.Create("Enterprise", "Enterprise", "Monthly", 999, "USD", 0.05m, null, null, "24/7");
        var sub = Subscription.Create(RetailerId, plan.Id, SubscriptionStatus.Active, DateTime.UtcNow);
        
        // Mock the Select projection manually since Moq.EntityFrameworkCore handles the collection
        // but not necessarily the Join/Select in a complex way.
        // Actually, PlanLimitService uses .Select(s => new { ... }) which is handled by the mock context.
        
        // We need to ensure the Plan navigation is set or the query will fail if it's not in the mock.
        // However, PlanLimitService uses .Select(s => new { s.Status, s.Plan.MaxActiveProducts, s.Plan.Name })
        
        // Let's use a real list and let Moq.EntityFrameworkCore handle it.
        var subs = new List<Subscription> { sub };
        // We can't easily set the Plan navigation property on a stub without reflection or a proper setup.
        typeof(Subscription).GetProperty(nameof(Subscription.Plan))!.SetValue(sub, plan);

        _contextMock.Setup(x => x.Subscriptions).ReturnsDbSet(subs);

        // Act
        await _sut.EnforceAsync(RetailerId);

        // Assert
        _contextMock.Verify(x => x.Products, Times.Never);
    }

    [Fact]
    public async Task EnforceAsync_LimitExceeded_ThrowsBusinessRuleException()
    {
        // Arrange
        var plan = SubscriptionPlan.Create("Basic", "Basic", "Monthly", 10, "USD", 0.05m, 2, 100, "Standard");
        var sub = Subscription.Create(RetailerId, plan.Id, SubscriptionStatus.Active, DateTime.UtcNow);
        typeof(Subscription).GetProperty(nameof(Subscription.Plan))!.SetValue(sub, plan);

        var products = new List<Product>
        {
            Product.Create(RetailerId, "P1", "", null, null, 10, "USD", null, ProductStatus.Active),
            Product.Create(RetailerId, "P2", "", null, null, 10, "USD", null, ProductStatus.Active)
        };

        _contextMock.Setup(x => x.Subscriptions).ReturnsDbSet(new List<Subscription> { sub });
        _contextMock.Setup(x => x.Products).ReturnsDbSet(products);

        // Act
        var act = () => _sut.EnforceAsync(RetailerId);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("PRODUCT_LIMIT_REACHED");
    }

    [Fact]
    public async Task EnforceAsync_UnderLimit_DoesNotThrow()
    {
        // Arrange
        var plan = SubscriptionPlan.Create("Basic", "Basic", "Monthly", 10, "USD", 0.05m, 10, 100, "Standard");
        var sub = Subscription.Create(RetailerId, plan.Id, SubscriptionStatus.Active, DateTime.UtcNow);
        typeof(Subscription).GetProperty(nameof(Subscription.Plan))!.SetValue(sub, plan);

        _contextMock.Setup(x => x.Subscriptions).ReturnsDbSet(new List<Subscription> { sub });
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product>());

        // Act & Assert
        await _sut.Invoking(x => x.EnforceAsync(RetailerId)).Should().NotThrowAsync();
    }
}
