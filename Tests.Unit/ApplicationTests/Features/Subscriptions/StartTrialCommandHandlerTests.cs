using Application.Features.Subscriptions.Commands.StartTrial;
using Application.Features.Subscriptions.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Subscriptions;

public sealed class StartTrialCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _userMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<Subscription>> _subscriptionRepoMock = new();
    private readonly Mock<IRepository<SubscriptionPlan>> _planRepoMock = new();
    private readonly StartTrialCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid TrialPlanId = new("11111111-1111-1111-1111-111111111001");

    public StartTrialCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<Subscription>()).Returns(_subscriptionRepoMock.Object);
        _uowMock.Setup(x => x.Repository<SubscriptionPlan>()).Returns(_planRepoMock.Object);
        _userMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _sut = new StartTrialCommandHandler(
            _uowMock.Object,
            _userMock.Object,
            _cacheMock.Object);
    }

    private static SubscriptionPlan CreateTrialPlan()
    {
        var plan = SubscriptionPlan.Create(
            "Basic Monthly", "Basic", "Monthly", 0m, "USD", 0.05m, 50, 100, "Standard");
        typeof(SubscriptionPlan)
            .GetProperty(nameof(SubscriptionPlan.Id))!
            .SetValue(plan, TrialPlanId);
        return plan;
    }

    private void SetupNoExistingSubscription()
    {
        _subscriptionRepoMock
            .Setup(x => x.AnyAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private void SetupSubscriptionAdd()
    {
        _subscriptionRepoMock
            .Setup(x => x.AddAsync(
                It.IsAny<Subscription>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription s, CancellationToken _) => s);
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _userMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new StartTrialCommand(), default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ExistingSubscriptionExists_ThrowsBusinessRuleException()
    {
        _subscriptionRepoMock
            .Setup(x => x.AnyAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _sut.Handle(new StartTrialCommand(), default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("TRIAL_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_TrialPlanNotFound_ThrowsNotFoundException()
    {
        SetupNoExistingSubscription();

        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var act = () => _sut.Handle(new StartTrialCommand(), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesTrialSubscription()
    {
        SetupNoExistingSubscription();
        SetupSubscriptionAdd();

        var plan = CreateTrialPlan();
        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        Subscription? capturedSubscription = null;
        _subscriptionRepoMock
            .Setup(x => x.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Callback<Subscription, CancellationToken>((s, _) => capturedSubscription = s)
            .ReturnsAsync((Subscription s, CancellationToken _) => s);

        await _sut.Handle(new StartTrialCommand(), default);

        capturedSubscription.Should().NotBeNull();
        capturedSubscription!.RetailerId.Should().Be(RetailerId);
        capturedSubscription.PlanId.Should().Be(TrialPlanId);
        capturedSubscription.Status.Should().Be(SubscriptionStatus.Trial);
        capturedSubscription.TrialEndsAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ValidRequest_TrialEndsInFourteenDays()
    {
        SetupNoExistingSubscription();

        var plan = CreateTrialPlan();
        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        Subscription? capturedSubscription = null;
        _subscriptionRepoMock
            .Setup(x => x.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Callback<Subscription, CancellationToken>((s, _) => capturedSubscription = s)
            .ReturnsAsync((Subscription s, CancellationToken _) => s);

        await _sut.Handle(new StartTrialCommand(), default);

        capturedSubscription!.TrialEndsAt.Should()
            .BeCloseTo(DateTime.UtcNow.AddDays(14), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsDtoWithTrialStatus()
    {
        SetupNoExistingSubscription();
        SetupSubscriptionAdd();

        var plan = CreateTrialPlan();
        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _sut.Handle(new StartTrialCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be(SubscriptionStatus.Trial);
        result.Data.PlanId.Should().Be(TrialPlanId);
        result.Data.IsInTrial.Should().BeTrue();
        result.Data.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Success_SavesChanges()
    {
        SetupNoExistingSubscription();
        SetupSubscriptionAdd();

        var plan = CreateTrialPlan();
        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        await _sut.Handle(new StartTrialCommand(), default);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Success_InvalidatesCacheByRetailerPrefix()
    {
        SetupNoExistingSubscription();
        SetupSubscriptionAdd();

        var plan = CreateTrialPlan();
        _planRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SubscriptionPlan, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        await _sut.Handle(new StartTrialCommand(), default);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(
                It.Is<string>(s => s.StartsWith($"subscriptions:{RetailerId}:")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}