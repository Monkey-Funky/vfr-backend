// tests/Tests.Unit/Application/Features/Subscriptions/GetCurrentSubscriptionQueryHandlerTests.cs
using Application.Features.Subscriptions.DTOs;
using Application.Features.Subscriptions.Queries.GetCurrentSubscription;
using Application.Interfaces;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Subscriptions;
using Domain.Enums;
using Domain.Enums.Subscription;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Subscriptions;

public sealed class GetCurrentSubscriptionQueryHandlerTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly GetCurrentSubscriptionQueryHandler _handler;

    public GetCurrentSubscriptionQueryHandlerTests()
    {
        _contextMock = MockRepository.Create<IApplicationDbContext>();
        _currentUserMock = MockRepository.Create<ICurrentUserService>();

        _handler = new GetCurrentSubscriptionQueryHandler(
            _contextMock.Object,
            _currentUserMock.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SubscriptionPlan BuildTestPlan()
        => SubscriptionPlan.Create(
            "Basic Monthly", "Basic", "Monthly",
            9.99m, "USD", 0.05m, 50, 1000, "Email");

    private static Subscription BuildActiveSubscription(Guid retailerId, SubscriptionPlan plan)
    {
        var sub = Subscription.Create(
            retailerId, plan.Id, SubscriptionStatus.Active,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30));

        // Set navigation property via reflection (private setter)
        typeof(Subscription).GetProperty("Plan")!.SetValue(sub, plan);
        return sub;
    }

    [Fact]
    public async Task Handle_NoSubscriptionForRetailer_ThrowsNotFoundException()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        var emptyDbSet = MockDbSetFactory.Create(new List<Subscription>());
        _contextMock.Setup(c => c.Subscriptions).Returns(emptyDbSet.Object);

        // Act
        Func<Task> act = () =>
            _handler.Handle(new GetCurrentSubscriptionQuery(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*subscription*");
    }

    [Fact]
    public async Task Handle_ExistingSubscription_ReturnsCorrectCurrentSubscriptionDto()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        SubscriptionPlan plan = BuildTestPlan();
        Subscription subscription = BuildActiveSubscription(retailerId, plan);

        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        var dbSet = MockDbSetFactory.Create(new List<Subscription> { subscription });
        _contextMock.Setup(c => c.Subscriptions).Returns(dbSet.Object);

        // Act
        CurrentSubscriptionDto result =
            await _handler.Handle(new GetCurrentSubscriptionQuery(), CancellationToken.None);

        // Assert
        result.SubscriptionId.Should().Be(subscription.Id);
        result.Status.Should().Be(SubscriptionStatus.Active);
        result.CurrentPlan.Should().NotBeNull();
        result.CurrentPlan.Name.Should().Be("Basic Monthly");
        result.IsActive.Should().BeTrue();
        result.CanUpgrade.Should().BeTrue("Active subscriptions can be upgraded");
        result.CanCancel.Should().BeTrue("Active subscriptions can be cancelled");
    }
}