// tests/Tests.Unit/Application/Features/Subscriptions/UpgradeSubscriptionCommandHandlerTests.cs
using Application.Features.Subscriptions.Commands.UpgradePlan;
using Application.Interfaces;
using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Common;
using Domain.Entities.Subscriptions;
using Domain.Enums;
using Domain.Enums.Subscription;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Subscriptions;

public sealed class UpgradeSubscriptionCommandHandlerTests : TestBase
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<Subscription>> _subRepoMock;
    private readonly Mock<IRepository<SubscriptionPlan>> _planRepoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IPaymentGatewayService> _paymentMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly UpgradePlanCommandHandler _handler;

    public UpgradeSubscriptionCommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _subRepoMock = MockRepository.Create<IRepository<Subscription>>();
        _planRepoMock = MockRepository.Create<IRepository<SubscriptionPlan>>();
        _currentUserMock = MockRepository.Create<ICurrentUserService>();
        _paymentMock = MockRepository.Create<IPaymentGatewayService>();
        _cacheMock = MockRepository.Create<ICacheService>();

        _unitOfWorkMock.Setup(u => u.Repository<Subscription>()).Returns(_subRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Repository<SubscriptionPlan>()).Returns(_planRepoMock.Object);

        _handler = new UpgradePlanCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserMock.Object,
            _paymentMock.Object,
            _cacheMock.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SubscriptionPlan BuildPlan(decimal price, string name = "TestPlan")
        => SubscriptionPlan.Create(name, "Basic", "Monthly", price, "USD", 0.05m, 50, 1000, "Email");

    private static Subscription BuildActiveSubscription(Guid retailerId, Guid planId)
        => Subscription.Create(retailerId, planId, SubscriptionStatus.Active,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30));

    [Fact]
    public async Task Handle_NewPlanPriceLowerThanCurrentPlan_ThrowsBusinessRuleException()
    {
        // Arrange — Pro→Free scenario: newPlan.PriceAmount <= currentPlan.PriceAmount
        Guid retailerId = Guid.NewGuid();
        Guid currentPlanId = Guid.NewGuid();
        Guid newPlanId = Guid.NewGuid();
        Guid paymentMethodId = Guid.NewGuid();

        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        var activeSubscription = BuildActiveSubscription(retailerId, currentPlanId);
        _subRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSubscription);

        // Current plan: Pro — 99.99 USD
        var currentPlan = BuildPlan(99.99m, "Pro Monthly");
        // New (lower) plan: Free — 0.00 USD
        var newPlan = BuildPlan(0.00m, "Free");

        typeof(BaseEntity).GetProperty("Id")!.SetValue(currentPlan, currentPlanId);
        typeof(BaseEntity).GetProperty("Id")!.SetValue(newPlan, newPlanId);

        _planRepoMock
            .SetupSequence(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentPlan)
            .ReturnsAsync(newPlan);

        var command = new UpgradePlanCommand(newPlanId, paymentMethodId);

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*UPGRADE_TO_LOWER_PLAN*");
    }

    [Fact]
    public async Task Handle_SubscriptionStatusNotActive_ThrowsBusinessRuleException()
    {
        // Arrange — subscription is Expired, upgrades are blocked
        Guid retailerId = Guid.NewGuid();
        Guid currentPlanId = Guid.NewGuid();
        Guid paymentMethodId = Guid.NewGuid();

        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        var expiredSub = Subscription.Create(
            retailerId, currentPlanId, SubscriptionStatus.Expired,
            DateTime.UtcNow.AddDays(-60), DateTime.UtcNow.AddDays(-1));

        _subRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredSub);

        var command = new UpgradePlanCommand(Guid.NewGuid(), paymentMethodId);

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*UPGRADE_NOT_ALLOWED*");
    }

    [Fact]
    public async Task Handle_SubscriptionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        _subRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        var command = new UpgradePlanCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*subscription*");
    }
}