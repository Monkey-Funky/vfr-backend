using Application.Features.PaymentMethods.Commands.AddPaymentMethod;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;

namespace Tests.Unit.Application.Features.PaymentMethods;

public sealed class AddPaymentMethodCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IEncryptionService> _encryptionServiceMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<IRepository<PaymentMethod>> _paymentMethodRepoMock = new();
    private readonly AddPaymentMethodCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private const string EncryptedName = "ENCRYPTED_ALICE_SMITH";

    private static readonly string FutureExpiry =
        $"{DateTime.UtcNow.Month:D2}/{DateTime.UtcNow.Year + 3}";

    public AddPaymentMethodCommandHandlerTests()
    {
        _sut = new AddPaymentMethodCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _encryptionServiceMock.Object,
            _cacheServiceMock.Object);

        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _unitOfWorkMock.Setup(x => x.Repository<PaymentMethod>())
            .Returns(_paymentMethodRepoMock.Object);

        _encryptionServiceMock.Setup(x => x.Encrypt(It.IsAny<string>()))
            .Returns(EncryptedName);

        _paymentMethodRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentMethod pm, CancellationToken _) => pm);

        _paymentMethodRepoMock.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PaymentMethod, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentMethod>().AsReadOnly());

        _paymentMethodRepoMock.Setup(x => x.UpdateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private AddPaymentMethodCommand ValidCommand(bool setAsDefault = false) =>
        new("Visa", "Alice Smith", "4242", FutureExpiry, "pm_test_stripe_123", true, setAsDefault);

    private PaymentMethod CreatePaymentMethod(bool isDefault = false)
    {
        var pm = PaymentMethod.Create(RetailerId, "Mastercard", EncryptedName, "9999", FutureExpiry, "pm_existing");
        if (isDefault) pm.SetAsDefault();
        return pm;
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Retailer identity*");
    }

    [Fact]
    public async Task Handle_ValidRequest_EncryptsCardholderName()
    {
        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _encryptionServiceMock.Verify(x => x.Encrypt("Alice Smith"), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_AddsPaymentMethodToRepository()
    {
        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _paymentMethodRepoMock.Verify(
            x => x.AddAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_PaymentMethodHasCorrectRetailerId()
    {
        PaymentMethod? captured = null;
        _paymentMethodRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentMethod, CancellationToken>((pm, _) => captured = pm)
            .ReturnsAsync((PaymentMethod pm, CancellationToken _) => pm);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        captured!.RetailerId.Should().Be(RetailerId);
    }

    [Fact]
    public async Task Handle_ValidRequest_PaymentMethodStoresEncryptedName()
    {
        PaymentMethod? captured = null;
        _paymentMethodRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentMethod, CancellationToken>((pm, _) => captured = pm)
            .ReturnsAsync((PaymentMethod pm, CancellationToken _) => pm);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        captured!.CardholderNameEncrypted.Should().Be(EncryptedName);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessWithPaymentMethodId()
    {
        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ValidRequest_SuccessMessageContainsProviderAndLast4()
    {
        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.Message.Should().Contain("Visa");
        result.Message.Should().Contain("4242");
    }

    [Fact]
    public async Task Handle_SetAsDefaultFalse_DoesNotQueryExistingDefaults()
    {
        await _sut.Handle(ValidCommand(setAsDefault: false), CancellationToken.None);

        _paymentMethodRepoMock.Verify(
            x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PaymentMethod, bool>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SetAsDefaultFalse_NewMethodIsNotDefault()
    {
        PaymentMethod? captured = null;
        _paymentMethodRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentMethod, CancellationToken>((pm, _) => captured = pm)
            .ReturnsAsync((PaymentMethod pm, CancellationToken _) => pm);

        await _sut.Handle(ValidCommand(setAsDefault: false), CancellationToken.None);

        captured!.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SetAsDefaultTrue_QueriesExistingDefaults()
    {
        await _sut.Handle(ValidCommand(setAsDefault: true), CancellationToken.None);

        _paymentMethodRepoMock.Verify(
            x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PaymentMethod, bool>>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SetAsDefaultTrue_WithExistingDefault_UnsetsExistingDefault()
    {
        var existingDefault = CreatePaymentMethod(isDefault: true);
        _paymentMethodRepoMock.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PaymentMethod, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentMethod> { existingDefault }.AsReadOnly());

        await _sut.Handle(ValidCommand(setAsDefault: true), CancellationToken.None);

        existingDefault.IsDefault.Should().BeFalse();
        _paymentMethodRepoMock.Verify(x => x.UpdateAsync(existingDefault, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SetAsDefaultTrue_NewMethodIsDefault()
    {
        PaymentMethod? captured = null;
        _paymentMethodRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentMethod, CancellationToken>((pm, _) => captured = pm)
            .ReturnsAsync((PaymentMethod pm, CancellationToken _) => pm);

        await _sut.Handle(ValidCommand(setAsDefault: true), CancellationToken.None);

        captured!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SetAsDefaultTrue_NoExistingDefault_NewMethodIsDefault()
    {
        PaymentMethod? captured = null;
        _paymentMethodRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentMethod, CancellationToken>((pm, _) => captured = pm)
            .ReturnsAsync((PaymentMethod pm, CancellationToken _) => pm);

        _paymentMethodRepoMock.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PaymentMethod, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentMethod>().AsReadOnly());

        await _sut.Handle(ValidCommand(setAsDefault: true), CancellationToken.None);

        captured!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidRequest_SaveChangesCalledExactlyOnce()
    {
        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnedIdMatchesCreatedPaymentMethod()
    {
        PaymentMethod? captured = null;
        _paymentMethodRepoMock.Setup(x => x.AddAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentMethod, CancellationToken>((pm, _) => captured = pm)
            .ReturnsAsync((PaymentMethod pm, CancellationToken _) => pm);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.Data.Should().Be(captured!.Id);
    }

    [Fact]
    public async Task Handle_SetAsDefaultTrue_MultipleExistingDefaults_AllUnset()
    {
        var default1 = CreatePaymentMethod(isDefault: true);
        var default2 = CreatePaymentMethod(isDefault: true);

        _paymentMethodRepoMock.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<PaymentMethod, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentMethod> { default1, default2 }.AsReadOnly());

        await _sut.Handle(ValidCommand(setAsDefault: true), CancellationToken.None);

        default1.IsDefault.Should().BeFalse();
        default2.IsDefault.Should().BeFalse();
        _paymentMethodRepoMock.Verify(x => x.UpdateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}