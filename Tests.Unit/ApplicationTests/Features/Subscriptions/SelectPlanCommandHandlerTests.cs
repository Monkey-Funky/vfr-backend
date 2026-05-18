using Application.Features.Subscriptions.Commands.SelectPlan;
using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Subscriptions;

public sealed class SelectPlanCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _userMock = new();
    private readonly Mock<IPaymentGatewayService> _paymentGatewayMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<SubscriptionPlan>> _planRepoMock = new();
    private readonly Mock<IRepository<PaymentMethod>> _paymentMethodRepoMock = new();
    private readonly Mock<IRepository<Subscription>> _subscriptionRepoMock = new();
    private readonly Mock<IRepository<SubscriptionPayment>> _paymentRepoMock = new();
    private readonly SelectPlanCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly string FutureExpiry = $"12/{DateTime.UtcNow.Year + 2}";

    public SelectPlanCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<SubscriptionPlan>()).Returns(_planRepoMock.Object);
        _uowMock.Setup(x => x.Repository<PaymentMethod>()).Returns(_paymentMethodRepoMock.Object);
        _uowMock.Setup(x => x.Repository<Subscription>()).Returns(_subscriptionRepoMock.Object);
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

        _subscriptionRepoMock
            .Setup(x => x.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription s, CancellationToken _) => s);

        _sut = new SelectPlanCommandHandler(
            _uowMock.Object,
            _userMock.Object,
            _paymentGatewayMock.Object,
            _cacheMock.Object);
    }

    private static SubscriptionPlan CreatePlan(string billingCycle = "Monthly", decimal price = 100m)
    {
        return SubscriptionPlan.Create(
            "Basic Monthly", "Basic", billingCycle, price, "USD", 0.05m, 50, 100, "Standard");
    }

    private static PaymentMethod CreateValidPaymentMethod()
    {
        return PaymentMethod.Create(
            RetailerId, "Visa", "encrypted_name", "4242", FutureExpiry, "pm_test_stripe_token");
    }

    private static PaymentMethod CreatePaymentMethodWithoutToken()
    {
        return PaymentMethod.Create(
            RetailerId, "Visa", "encrypted_name", "4242", FutureExpiry, null);
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

    private void SetupSuccessfulStripeCharge()
    {
        _paymentGatewayMock
            .Setup(x => x.ChargeAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "pi_test_intent_id", null));
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _userMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        var command = new SelectPlanCommand(Guid.NewGuid(), Guid.NewGuid(), "Monthly");

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_PlanNotFound_ThrowsNotFoundException()
    {
        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var command = new SelectPlanCommand(Guid.NewGuid(), Guid.NewGuid(), "Monthly");

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_BillingCycleMismatch_ThrowsBusinessRuleException()
    {
        var plan = CreatePlan(billingCycle: "Yearly");

        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var command = new SelectPlanCommand(plan.Id, Guid.NewGuid(), "Monthly");

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("BILLING_CYCLE_MISMATCH");
    }

    [Fact]
    public async Task Handle_PaymentMethodNotFound_ThrowsNotFoundException()
    {
        var plan = CreatePlan();

        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentMethod?)null);

        var command = new SelectPlanCommand(plan.Id, Guid.NewGuid(), "Monthly");

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PaymentMethodNotTokenized_ThrowsBusinessRuleException()
    {
        var plan = CreatePlan();
        var pm = CreatePaymentMethodWithoutToken();

        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        var command = new SelectPlanCommand(plan.Id, pm.Id, "Monthly");

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("PAYMENT_METHOD_NOT_TOKENIZED");
    }

    [Fact]
    public async Task Handle_PaymentMethodExpired_ThrowsBusinessRuleException()
    {
        var plan = CreatePlan();
        var pm = CreateExpiredPaymentMethod();

        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        var command = new SelectPlanCommand(plan.Id, pm.Id, "Monthly");

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("PAYMENT_METHOD_EXPIRED");
    }

    [Fact]
    public async Task Handle_PlanAlreadyActive_ThrowsBusinessRuleException()
    {
        var plan = CreatePlan();
        var pm = CreateValidPaymentMethod();
        var existingSubscription = Subscription.Create(
            RetailerId, plan.Id, SubscriptionStatus.Active,
            DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddMonths(11));

        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSubscription);

        var command = new SelectPlanCommand(plan.Id, pm.Id, "Monthly");

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("PLAN_ALREADY_ACTIVE");
    }

    [Fact]
    public async Task Handle_StripeChargeFails_ThrowsExternalServiceException()
    {
        var plan = CreatePlan();
        var pm = CreateValidPaymentMethod();

        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Subscription>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        _paymentGatewayMock
            .Setup(x => x.ChargeAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(false, null, "Insufficient funds."));

        var command = new SelectPlanCommand(plan.Id, pm.Id, "Monthly");

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task Handle_NewSubscription_CreatesSubscriptionAndReturnsId()
    {
        var plan = CreatePlan();
        var pm = CreateValidPaymentMethod();

        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Subscription>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        SetupSuccessfulStripeCharge();

        var command = new SelectPlanCommand(plan.Id, pm.Id, "Monthly");

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);

        _subscriptionRepoMock.Verify(
            x => x.AddAsync(
                It.Is<Subscription>(s => s.RetailerId == RetailerId && s.PlanId == plan.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingTrialSubscription_ActivatesAndInvalidatesCache()
    {
        var plan = CreatePlan();
        var pm = CreateValidPaymentMethod();
        var existingPlanId = Guid.NewGuid();
        var trialSubscription = Subscription.Create(
            RetailerId, existingPlanId, SubscriptionStatus.Trial,
            DateTime.UtcNow.AddDays(-7), null, DateTime.UtcNow.AddDays(7));

        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(trialSubscription);

        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Subscription>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trialSubscription);

        SetupSuccessfulStripeCharge();

        var command = new SelectPlanCommand(plan.Id, pm.Id, "Monthly");

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        trialSubscription.Status.Should().Be(SubscriptionStatus.Active);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(
                It.Is<string>(s => s.StartsWith($"subscriptions:{RetailerId}:")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Success_InvalidatesCacheByRetailerPrefix()
    {
        var plan = CreatePlan();
        var pm = CreateValidPaymentMethod();

        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);

        _subscriptionRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Subscription>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        SetupSuccessfulStripeCharge();

        var command = new SelectPlanCommand(plan.Id, pm.Id, "Monthly");

        await _sut.Handle(command, default);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(
                It.Is<string>(s => s.StartsWith($"subscriptions:{RetailerId}:")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}