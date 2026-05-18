using Application.Features.Subscriptions.Commands.UpgradePlan;
using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Subscriptions;

public sealed class UpgradePlanCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _userMock = new();
    private readonly Mock<IPaymentGatewayService> _paymentGatewayMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<Subscription>> _subscriptionRepoMock = new();
    private readonly Mock<IRepository<SubscriptionPlan>> _planRepoMock = new();
    private readonly Mock<IRepository<PaymentMethod>> _paymentMethodRepoMock = new();
    private readonly Mock<IRepository<SubscriptionPayment>> _paymentRepoMock = new();
    private readonly UpgradePlanCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly string FutureExpiry = $"12/{DateTime.UtcNow.Year + 2}";

    public UpgradePlanCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<Subscription>()).Returns(_subscriptionRepoMock.Object);
        _uowMock.Setup(x => x.Repository<SubscriptionPlan>()).Returns(_planRepoMock.Object);
        _uowMock.Setup(x => x.Repository<PaymentMethod>()).Returns(_paymentMethodRepoMock.Object);
        _uowMock.Setup(x => x.Repository<SubscriptionPayment>()).Returns(_paymentRepoMock.Object);

        _uowMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> action, CancellationToken ct) => action(ct));

        _userMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _paymentRepoMock
            .Setup(x => x.AddAsync(It.IsAny<SubscriptionPayment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPayment p, CancellationToken _) => p);

        _sut = new UpgradePlanCommandHandler(
            _uowMock.Object,
            _userMock.Object,
            _paymentGatewayMock.Object,
            _cacheMock.Object);
    }

    private static Subscription CreateActiveSubscription(Guid? planId = null)
    {
        return Subscription.Create(
            RetailerId,
            planId ?? Guid.NewGuid(),
            SubscriptionStatus.Active,
            DateTime.UtcNow.AddMonths(-1),
            DateTime.UtcNow.AddMonths(11));
    }

    private static SubscriptionPlan CreatePlan(decimal price = 100m, string billingCycle = "Monthly")
    {
        return SubscriptionPlan.Create(
            "Test Plan", "Standard", billingCycle, price, "USD", 0.05m, 100, 500, "Premium");
    }

    private static PaymentMethod CreateValidPaymentMethod()
    {
        return PaymentMethod.Create(
            RetailerId, "Visa", "encrypted_name", "4242", FutureExpiry, "pm_test_stripe_token");
    }

    private static PaymentMethod CreateExpiredPaymentMethod()
    {
        var pm = PaymentMethod.Create(
            RetailerId, "Visa", "encrypted_name", "4242", FutureExpiry, "pm_test_stripe_token");
        typeof(PaymentMethod)
            .GetProperty(nameof(PaymentMethod.ExpiresAt))!
            .SetValue(pm, DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-1));
        return pm;
    }

    private void SetupPlansSequence(SubscriptionPlan? currentPlan, SubscriptionPlan? newPlan)
    {
        _planRepoMock
            .SetupSequence(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentPlan)
            .ReturnsAsync(newPlan);
    }

    private void SetupSuccessfulStripeCharge()
    {
        _paymentGatewayMock
            .Setup(x => x.ChargeAsync(
                It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "pi_test_intent", null));
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _userMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        var command = new UpgradePlanCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_SubscriptionNotFound_ThrowsNotFoundException()
    {
        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        var command = new UpgradePlanCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(SubscriptionStatus.Trial)]
    [InlineData(SubscriptionStatus.Cancelled)]
    [InlineData(SubscriptionStatus.Expired)]
    [InlineData(SubscriptionStatus.None)]
    public async Task Handle_SubscriptionNotInAllowedStatus_ThrowsBusinessRuleException(SubscriptionStatus status)
    {
        var subscription = Subscription.Create(
            RetailerId, Guid.NewGuid(), status,
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddMonths(11));

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var command = new UpgradePlanCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("UPGRADE_NOT_ALLOWED");
    }

    [Fact]
    public async Task Handle_CurrentPlanNotFound_ThrowsNotFoundException()
    {
        var subscription = CreateActiveSubscription();

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var command = new UpgradePlanCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NewPlanNotFound_ThrowsNotFoundException()
    {
        var currentPlan = CreatePlan(price: 100m);
        var subscription = CreateActiveSubscription();

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SetupPlansSequence(currentPlan, null);

        var command = new UpgradePlanCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NewPlanHasLowerOrEqualPrice_ThrowsBusinessRuleException()
    {
        var currentPlan = CreatePlan(price: 200m);
        var newPlan = CreatePlan(price: 100m);
        var subscription = CreateActiveSubscription();

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SetupPlansSequence(currentPlan, newPlan);

        var command = new UpgradePlanCommand(newPlan.Id, Guid.NewGuid());

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("UPGRADE_TO_LOWER_PLAN");
    }

    [Fact]
    public async Task Handle_PaymentMethodNotFound_ThrowsNotFoundException()
    {
        var currentPlan = CreatePlan(price: 100m);
        var newPlan = CreatePlan(price: 200m);
        var subscription = CreateActiveSubscription();

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SetupPlansSequence(currentPlan, newPlan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentMethod?)null);

        var command = new UpgradePlanCommand(newPlan.Id, Guid.NewGuid());

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PaymentMethodNotTokenized_ThrowsBusinessRuleException()
    {
        var currentPlan = CreatePlan(price: 100m);
        var newPlan = CreatePlan(price: 200m);
        var subscription = CreateActiveSubscription();
        var pm = PaymentMethod.Create(RetailerId, "Visa", "enc", "4242", FutureExpiry, null);

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SetupPlansSequence(currentPlan, newPlan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        var command = new UpgradePlanCommand(newPlan.Id, pm.Id);

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("PAYMENT_METHOD_NOT_TOKENIZED");
    }

    [Fact]
    public async Task Handle_PaymentMethodExpired_ThrowsBusinessRuleException()
    {
        var currentPlan = CreatePlan(price: 100m);
        var newPlan = CreatePlan(price: 200m);
        var subscription = CreateActiveSubscription();
        var pm = CreateExpiredPaymentMethod();

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SetupPlansSequence(currentPlan, newPlan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        var command = new UpgradePlanCommand(newPlan.Id, pm.Id);

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("PAYMENT_METHOD_EXPIRED");
    }

    [Fact]
    public async Task Handle_StripeChargeFails_ThrowsExternalServiceException()
    {
        var currentPlan = CreatePlan(price: 100m);
        var newPlan = CreatePlan(price: 200m);
        var subscription = CreateActiveSubscription();
        var pm = CreateValidPaymentMethod();

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SetupPlansSequence(currentPlan, newPlan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Subscription>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _paymentGatewayMock
            .Setup(x => x.ChargeAsync(
                It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(false, null, "Card declined."));

        var command = new UpgradePlanCommand(newPlan.Id, pm.Id);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task Handle_ValidUpgrade_UpgradesPlanAndReturnsSuccess()
    {
        var currentPlan = CreatePlan(price: 100m);
        var newPlan = CreatePlan(price: 200m);
        var subscription = CreateActiveSubscription(planId: currentPlan.Id);
        var pm = CreateValidPaymentMethod();

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SetupPlansSequence(currentPlan, newPlan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Subscription>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SetupSuccessfulStripeCharge();

        var command = new UpgradePlanCommand(newPlan.Id, pm.Id);

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        subscription.PlanId.Should().Be(newPlan.Id);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task Handle_PendingDowngradeSubscription_AllowsUpgrade()
    {
        var currentPlanId = Guid.NewGuid();
        var pendingDowngradePlanId = Guid.NewGuid();
        var subscription = Subscription.Create(
            RetailerId, currentPlanId, SubscriptionStatus.Active,
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddMonths(11));
        subscription.SetPendingDowngrade(pendingDowngradePlanId, DateTime.UtcNow.AddMonths(11));

        var currentPlan = CreatePlan(price: 100m);
        var newPlan = CreatePlan(price: 300m);
        var pm = CreateValidPaymentMethod();

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SetupPlansSequence(currentPlan, newPlan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Subscription>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SetupSuccessfulStripeCharge();

        var command = new UpgradePlanCommand(newPlan.Id, pm.Id);

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        subscription.PlanId.Should().Be(newPlan.Id);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.PendingDowngradePlanId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Success_InvalidatesCacheByRetailerPrefix()
    {
        var currentPlan = CreatePlan(price: 100m);
        var newPlan = CreatePlan(price: 200m);
        var subscription = CreateActiveSubscription(planId: currentPlan.Id);
        var pm = CreateValidPaymentMethod();

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SetupPlansSequence(currentPlan, newPlan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Subscription>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        SetupSuccessfulStripeCharge();

        var command = new UpgradePlanCommand(newPlan.Id, pm.Id);

        await _sut.Handle(command, default);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(
                It.Is<string>(s => s.StartsWith($"subscriptions:{RetailerId}:")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}