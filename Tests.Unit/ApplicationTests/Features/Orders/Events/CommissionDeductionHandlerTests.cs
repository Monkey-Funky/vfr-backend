using Application.Features.Orders.Events;
using Application.Interfaces.Persistence;
using Domain.Entities.Notifications;
using Domain.Entities.Subscriptions;
using Domain.Enums.Orders;
using Domain.Enums.Subscription;
using Domain.Events;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.ApplicationTests.Features.Orders.Events;

public sealed class CommissionDeductionHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ILogger<CommissionDeductionHandler>> _loggerMock = new();
    private readonly Mock<IRepository<CommissionRecord>> _commissionRepoMock = new();
    private readonly Mock<IRepository<Notification>> _notificationRepoMock = new();
    private readonly CommissionDeductionHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public CommissionDeductionHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Repository<CommissionRecord>()).Returns(_commissionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Repository<Notification>()).Returns(_notificationRepoMock.Object);

        _commissionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<CommissionRecord>(), It.IsAny<CancellationToken>()))
            .Returns((CommissionRecord r, CancellationToken _) => Task.FromResult(r));

        _notificationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns((Notification n, CancellationToken _) => Task.FromResult(n));

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _sut = new CommissionDeductionHandler(_unitOfWorkMock.Object, _contextMock.Object, _loggerMock.Object);
    }

    private static SubscriptionPlan BuildPlan(decimal commissionRate = 0.05m, string currency = "EGP")
    {
        return SubscriptionPlan.Create(
            name: "Standard Monthly",
            tier: "Standard",
            billingCycle: "Monthly",
            priceAmount: 199m,
            currency: currency,
            commissionRate: commissionRate,
            maxActiveProducts: 200,
            maxMonthlyTryOns: 2000,
            supportLevel: "Standard");
    }

    private static Subscription BuildActiveSubscription(Guid retailerId, Guid planId, SubscriptionPlan plan)
    {
        var subscription = Subscription.Create(
            retailerId: retailerId,
            planId: planId,
            initialStatus: SubscriptionStatus.Active,
            startDate: DateTime.UtcNow.AddDays(-30),
            endDate: DateTime.UtcNow.AddDays(30));

        typeof(Subscription).GetProperty("Plan")!.SetValue(subscription, plan);
        return subscription;
    }

    private void SetupSubscriptionsDbSet(List<Subscription> subscriptions)
    {
        var mockDbSet = subscriptions.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.Subscriptions).Returns(mockDbSet.Object);
    }

    private static OrderStatusChangedEvent BuildDeliveredEvent(decimal unitPrice = 500m, int quantity = 2)
    {
        var item = OrderItem.Create(OrderId, ProductId, "Test Product", unitPrice, quantity);
        return new OrderStatusChangedEvent(
            OrderId: OrderId,
            RetailerId: RetailerId,
            PreviousStatus: OrderStatus.Shipped,
            NewStatus: OrderStatus.Delivered,
            Items: [item]);
    }

    [Fact]
    public async Task Handle_OrderDeliveredEvent_CalculatesCommissionCorrectly()
    {
        var plan = BuildPlan(commissionRate: 0.05m);
        var subscription = BuildActiveSubscription(RetailerId, PlanId, plan);
        SetupSubscriptionsDbSet([subscription]);

        CommissionRecord? capturedRecord = null;
        _commissionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<CommissionRecord>(), It.IsAny<CancellationToken>()))
            .Callback<CommissionRecord, CancellationToken>((r, _) => capturedRecord = r)
            .Returns((CommissionRecord r, CancellationToken _) => Task.FromResult(r));

        var @event = BuildDeliveredEvent(unitPrice: 500m, quantity: 2);

        await _sut.Handle(@event, CancellationToken.None);

        capturedRecord.Should().NotBeNull();
        capturedRecord!.CommissionAmount.Should().Be(Math.Round(1000m * 0.05m, 2));
        capturedRecord.OrderTotal.Should().Be(1000m);
        capturedRecord.CommissionRate.Should().Be(0.05m);
    }

    [Fact]
    public async Task Handle_OrderDeliveredEvent_CreatesCommissionRecord()
    {
        var plan = BuildPlan();
        var subscription = BuildActiveSubscription(RetailerId, PlanId, plan);
        SetupSubscriptionsDbSet([subscription]);

        var @event = BuildDeliveredEvent();

        await _sut.Handle(@event, CancellationToken.None);

        _commissionRepoMock.Verify(
            r => r.AddAsync(It.IsAny<CommissionRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OrderDeliveredEvent_DeductsFromRetailerBalance()
    {
        var plan = BuildPlan(commissionRate: 0.05m, currency: "EGP");
        var subscription = BuildActiveSubscription(RetailerId, PlanId, plan);
        SetupSubscriptionsDbSet([subscription]);

        Notification? capturedNotification = null;
        _notificationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => capturedNotification = n)
            .Returns((Notification n, CancellationToken _) => Task.FromResult(n));

        var @event = BuildDeliveredEvent(unitPrice: 500m, quantity: 2);

        await _sut.Handle(@event, CancellationToken.None);

        capturedNotification.Should().NotBeNull();
        capturedNotification!.Body.Should().Contain("50.00");
        capturedNotification.Body.Should().Contain("deducted from your balance");
        capturedNotification.Title.Should().Be("Commission Deducted");
    }

    [Fact]
    public async Task Handle_OrderDeliveredEvent_PersistsChanges()
    {
        var plan = BuildPlan();
        var subscription = BuildActiveSubscription(RetailerId, PlanId, plan);
        SetupSubscriptionsDbSet([subscription]);

        var @event = BuildDeliveredEvent();

        await _sut.Handle(@event, CancellationToken.None);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ThrowsNotFoundException()
    {
        _contextMock
            .Setup(c => c.Subscriptions)
            .Throws(new NotFoundException("Subscription", RetailerId));

        var @event = BuildDeliveredEvent();
        var act = () => _sut.Handle(@event, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}