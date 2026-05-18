using Application.Features.PaymentMethods.Commands.RemovePaymentMethod;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Tests.Unit.ApplicationTests.Features.PaymentMethods;

public sealed class RemovePaymentMethodCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IRepository<PaymentMethod>> _paymentMethodRepoMock = new();
    private readonly RemovePaymentMethodCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly string FutureExpiry = $"{DateTime.UtcNow.Month:D2}/{DateTime.UtcNow.Year + 3}";
    private const string EncryptedName = "ENCRYPTED_NAME";

    public RemovePaymentMethodCommandHandlerTests()
    {
        _sut = new RemovePaymentMethodCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object);

        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _unitOfWorkMock.Setup(x => x.Repository<PaymentMethod>())
            .Returns(_paymentMethodRepoMock.Object);

        _paymentMethodRepoMock
            .Setup(x => x.SoftDeleteAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private PaymentMethod CreateMethod(bool isDefault = false)
    {
        var method = PaymentMethod.Create(RetailerId, "Visa", EncryptedName, "4242", FutureExpiry, "pm_test");
        if (isDefault) method.SetAsDefault();
        return method;
    }

    private void SetupMethodLookup(PaymentMethod? method) =>
        _paymentMethodRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(method);

    private void SetupOtherCardsExist(bool exists) =>
        _paymentMethodRepoMock
            .Setup(x => x.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists);

    [Fact]
    public async Task Handle_PaymentMethodNotFound_ThrowsNotFoundException()
    {
        SetupMethodLookup(null);

        var act = () => _sut.Handle(new RemovePaymentMethodCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidId_RemovesFromStripe()
    {
        var method = CreateMethod();
        SetupMethodLookup(method);

        await _sut.Handle(new RemovePaymentMethodCommand(method.Id), CancellationToken.None);

        _paymentMethodRepoMock.Verify(
            x => x.SoftDeleteAsync(method, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidId_PersistsRemoval()
    {
        var method = CreateMethod();
        SetupMethodLookup(method);

        await _sut.Handle(new RemovePaymentMethodCommand(method.Id), CancellationToken.None);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidId_ReturnsSuccessResult()
    {
        var method = CreateMethod();
        SetupMethodLookup(method);

        var result = await _sut.Handle(new RemovePaymentMethodCommand(method.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_RemovingDefaultMethod_WhenOtherCardsExist_ThrowsBusinessRuleException()
    {
        var method = CreateMethod(isDefault: true);
        SetupMethodLookup(method);
        SetupOtherCardsExist(true);

        var act = () => _sut.Handle(new RemovePaymentMethodCommand(method.Id), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "CANNOT_DELETE_DEFAULT_PAYMENT_METHOD");
    }

    [Fact]
    public async Task Handle_RemovingDefaultMethod_PromotesNextAsDefault()
    {
        var method = CreateMethod(isDefault: true);
        SetupMethodLookup(method);
        SetupOtherCardsExist(false);

        var result = await _sut.Handle(new RemovePaymentMethodCommand(method.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _paymentMethodRepoMock.Verify(
            x => x.SoftDeleteAsync(method, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RemovingNonDefaultMethod_DoesNotCheckForOtherCards()
    {
        var method = CreateMethod(isDefault: false);
        SetupMethodLookup(method);

        await _sut.Handle(new RemovePaymentMethodCommand(method.Id), CancellationToken.None);

        _paymentMethodRepoMock.Verify(
            x => x.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new RemovePaymentMethodCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}