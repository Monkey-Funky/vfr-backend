using Application.Features.PaymentMethods.DTOs;
using Application.Features.PaymentMethods.Queries.GetPaymentMethods;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using MockQueryable.Moq;

namespace Tests.Unit.ApplicationTests.Features.PaymentMethods;

public sealed class GetPaymentMethodsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IEncryptionService> _encryptionServiceMock = new();
    private readonly GetPaymentMethodsQueryHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OtherRetailerId = Guid.NewGuid();
    private const string DecryptedName = "Alice Smith";
    private static readonly string FutureExpiry = $"{DateTime.UtcNow.Month:D2}/{DateTime.UtcNow.Year + 3}";

    public GetPaymentMethodsQueryHandlerTests()
    {
        _sut = new GetPaymentMethodsQueryHandler(
            _contextMock.Object,
            _currentUserServiceMock.Object,
            _encryptionServiceMock.Object);

        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _encryptionServiceMock
            .Setup(x => x.Decrypt(It.IsAny<string>()))
            .Returns(DecryptedName);
    }

    private static PaymentMethod CreatePaymentMethod(
        Guid? retailerId = null,
        bool isDefault = false,
        bool isDeleted = false)
    {
        var pm = PaymentMethod.Create(
            retailerId ?? RetailerId,
            "Visa",
            "ENCRYPTED_NAME",
            "4242",
            FutureExpiry,
            "pm_test_stripe");

        if (isDefault) pm.SetAsDefault();
        if (isDeleted) pm.MarkAsDeleted();

        return pm;
    }

    private void SetupPaymentMethodsDbSet(List<PaymentMethod> methods)
    {
        var mockSet = methods.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.PaymentMethods).Returns(mockSet.Object);
    }

    [Fact]
    public async Task Handle_NoPaymentMethods_ReturnsEmptyList()
    {
        SetupPaymentMethodsDbSet([]);

        var result = await _sut.Handle(new GetPaymentMethodsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PaymentMethodsExist_ReturnsMappedList()
    {
        var pm1 = CreatePaymentMethod();
        var pm2 = CreatePaymentMethod();
        SetupPaymentMethodsDbSet([pm1, pm2]);

        var result = await _sut.Handle(new GetPaymentMethodsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(dto =>
        {
            dto.CardholderName.Should().Be(DecryptedName);
            dto.ProviderType.Should().Be("Visa");
        });
    }

    [Fact]
    public async Task Handle_RetailerSeesOnlyOwnMethods()
    {
        var ownMethod = CreatePaymentMethod(RetailerId);
        var otherMethod = CreatePaymentMethod(OtherRetailerId);
        SetupPaymentMethodsDbSet([ownMethod, otherMethod]);

        var result = await _sut.Handle(new GetPaymentMethodsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_DefaultMethodIsMarkedCorrectly()
    {
        var defaultMethod = CreatePaymentMethod(isDefault: true);
        var nonDefaultMethod = CreatePaymentMethod(isDefault: false);
        SetupPaymentMethodsDbSet([nonDefaultMethod, defaultMethod]);

        var result = await _sut.Handle(new GetPaymentMethodsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.First().IsDefault.Should().BeTrue();
        result.Last().IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SoftDeletedMethods_AreExcluded()
    {
        var activeMethod = CreatePaymentMethod();
        var deletedMethod = CreatePaymentMethod(isDeleted: true);
        SetupPaymentMethodsDbSet([activeMethod, deletedMethod]);

        var result = await _sut.Handle(new GetPaymentMethodsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_MissingRetailerId_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        SetupPaymentMethodsDbSet([]);

        var act = () => _sut.Handle(new GetPaymentMethodsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_CardholderNameIsDecrypted()
    {
        var pm = CreatePaymentMethod();
        SetupPaymentMethodsDbSet([pm]);

        var result = await _sut.Handle(new GetPaymentMethodsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        _encryptionServiceMock.Verify(x => x.Decrypt("ENCRYPTED_NAME"), Times.Once);
        result[0].CardholderName.Should().Be(DecryptedName);
    }
}