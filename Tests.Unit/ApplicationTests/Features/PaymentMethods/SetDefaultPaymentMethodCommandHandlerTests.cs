using Application.Features.PaymentMethods.Commands.SetDefaultPaymentMethod;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Tests.Unit.ApplicationTests.Features.PaymentMethods;

public sealed class SetDefaultPaymentMethodCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IRepository<PaymentMethod>> _paymentMethodRepoMock = new();
    private readonly SetDefaultPaymentMethodCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly string FutureExpiry = $"{DateTime.UtcNow.Month:D2}/{DateTime.UtcNow.Year + 3}";
    private const string EncryptedName = "ENCRYPTED_NAME";

    public SetDefaultPaymentMethodCommandHandlerTests()
    {
        _sut = new SetDefaultPaymentMethodCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object);

        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _unitOfWorkMock.Setup(x => x.Repository<PaymentMethod>())
            .Returns(_paymentMethodRepoMock.Object);

        _paymentMethodRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _unitOfWorkMock
            .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (op, ct) => await op(ct));
    }

    private PaymentMethod CreateMethod(bool isDefault = false, string last4 = "4242")
    {
        var method = PaymentMethod.Create(RetailerId, "Visa", EncryptedName, last4, FutureExpiry, "pm_test");
        if (isDefault) method.SetAsDefault();
        return method;
    }

    private void SetupAllMethods(IReadOnlyList<PaymentMethod> methods) =>
        _paymentMethodRepoMock
            .Setup(x => x.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<PaymentMethod, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(methods);

    [Fact]
    public async Task Handle_PaymentMethodNotFound_ThrowsNotFoundException()
    {
        var otherMethod = CreateMethod();
        SetupAllMethods(new List<PaymentMethod> { otherMethod }.AsReadOnly());

        var act = () => _sut.Handle(new SetDefaultPaymentMethodCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NoPaymentMethodsForRetailer_ThrowsNotFoundException()
    {
        SetupAllMethods(new List<PaymentMethod>().AsReadOnly());

        var act = () => _sut.Handle(new SetDefaultPaymentMethodCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidId_SetsAsDefaultInStripe()
    {
        var method = CreateMethod(isDefault: false);
        SetupAllMethods(new List<PaymentMethod> { method }.AsReadOnly());

        await _sut.Handle(new SetDefaultPaymentMethodCommand(method.Id), CancellationToken.None);

        method.IsDefault.Should().BeTrue();
        _paymentMethodRepoMock.Verify(
            x => x.UpdateAsync(method, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidId_UpdatesIsDefaultFlag()
    {
        var method = CreateMethod(isDefault: false);
        SetupAllMethods(new List<PaymentMethod> { method }.AsReadOnly());

        var result = await _sut.Handle(new SetDefaultPaymentMethodCommand(method.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        method.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PreviousDefault_IsUnsetCorrectly()
    {
        var previousDefault = CreateMethod(isDefault: true, last4: "1111");
        var newDefault = CreateMethod(isDefault: false, last4: "2222");
        SetupAllMethods(new List<PaymentMethod> { previousDefault, newDefault }.AsReadOnly());

        await _sut.Handle(new SetDefaultPaymentMethodCommand(newDefault.Id), CancellationToken.None);

        previousDefault.IsDefault.Should().BeFalse();
        newDefault.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PreviousDefault_IsUnsetAndUpdatedInRepository()
    {
        var previousDefault = CreateMethod(isDefault: true, last4: "1111");
        var newDefault = CreateMethod(isDefault: false, last4: "2222");
        SetupAllMethods(new List<PaymentMethod> { previousDefault, newDefault }.AsReadOnly());

        await _sut.Handle(new SetDefaultPaymentMethodCommand(newDefault.Id), CancellationToken.None);

        _paymentMethodRepoMock.Verify(
            x => x.UpdateAsync(previousDefault, It.IsAny<CancellationToken>()),
            Times.Once);
        _paymentMethodRepoMock.Verify(
            x => x.UpdateAsync(newDefault, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_TargetIsAlreadyDefault_ReturnsSuccessWithoutChanges()
    {
        var method = CreateMethod(isDefault: true);
        SetupAllMethods(new List<PaymentMethod> { method }.AsReadOnly());

        var result = await _sut.Handle(new SetDefaultPaymentMethodCommand(method.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _unitOfWorkMock.Verify(
            x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new SetDefaultPaymentMethodCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_MultiplePreviousDefaults_AllAreUnset()
    {
        var default1 = CreateMethod(isDefault: true, last4: "1111");
        var default2 = CreateMethod(isDefault: true, last4: "2222");
        var target = CreateMethod(isDefault: false, last4: "3333");
        SetupAllMethods(new List<PaymentMethod> { default1, default2, target }.AsReadOnly());

        await _sut.Handle(new SetDefaultPaymentMethodCommand(target.Id), CancellationToken.None);

        default1.IsDefault.Should().BeFalse();
        default2.IsDefault.Should().BeFalse();
        target.IsDefault.Should().BeTrue();
    }
}