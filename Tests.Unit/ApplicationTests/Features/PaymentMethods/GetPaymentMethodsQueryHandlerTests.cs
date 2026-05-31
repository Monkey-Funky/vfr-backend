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
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly GetPaymentMethodsQueryHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OtherRetailerId = Guid.NewGuid();
    private const string DecryptedName = "Alice Smith";
    private static readonly string FutureExpiry =
        $"{DateTime.UtcNow.Month:D2}/{DateTime.UtcNow.Year + 3}";

    public GetPaymentMethodsQueryHandlerTests()
    {
        // Constructor order: context, currentUserService, encryptionService, cacheService
        _sut = new GetPaymentMethodsQueryHandler(
            _contextMock.Object,
            _currentUserServiceMock.Object,
            _encryptionServiceMock.Object,
            _cacheServiceMock.Object);

        _currentUserServiceMock
            .SetupGet(x => x.RetailerId)
            .Returns(RetailerId);

        _encryptionServiceMock
            .Setup(x => x.Decrypt(It.IsAny<string>()))
            .Returns(DecryptedName);

        // GetAsync<T> and SetAsync<T> have a `where T : class` constraint —
        // It.IsAnyType cannot be used as a generic argument (causes the
        // "ISetup does not contain ReturnsAsync" compiler error).
        // Leaving them unregistered causes Moq to return the Task<T?> default
        // which is null — exactly a cache miss, so every test hits the DB path.
        _cacheServiceMock
            .Setup(x => x.RemoveAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock
            .Setup(x => x.RemoveByPrefixAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock
            .Setup(x => x.RemoveByPatternAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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

    // ── Tests ─────────────────────────────────────────────────────────────────

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
    public async Task Handle_DefaultMethodIsOrderedFirst()
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

    [Fact]
    public async Task Handle_CacheMiss_FetchesFromDatabaseAndCachesResult()
    {
        var pm = CreatePaymentMethod();
        SetupPaymentMethodsDbSet([pm]);

        var result = await _sut.Handle(new GetPaymentMethodsQuery(), CancellationToken.None);

        // DB data must be returned on a cache miss.
        result.Should().HaveCount(1);

        // Handler must call SetAsync exactly once to populate the cache after a DB fetch.
        _cacheServiceMock.Verify(
            x => x.SetAsync(
                It.Is<string>(k => k.Contains(RetailerId.ToString("N"))),
                It.IsAny<List<PaymentMethodDto>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}