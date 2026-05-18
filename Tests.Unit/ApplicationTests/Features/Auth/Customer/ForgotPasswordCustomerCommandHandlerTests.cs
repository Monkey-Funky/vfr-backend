using Application.Features.Customer.Auth.Commands.ForgotPassword;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Customer;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Customer.Auth;

public sealed class ForgotPasswordCustomerCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<ForgotPasswordCustomerCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<CustomerAccount>> _customerRepoMock = new();
    private readonly ForgotPasswordCustomerCommandHandler _sut;

    private const string ValidEmail = "customer@example.com";

    public ForgotPasswordCustomerCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<CustomerAccount>()).Returns(_customerRepoMock.Object);
        _cacheServiceMock.Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _emailServiceMock.Setup(x => x.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new ForgotPasswordCustomerCommandHandler(
            _uowMock.Object,
            _cacheServiceMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    private CustomerAccount CreateActiveCustomer()
    {
        var customer = CustomerAccount.Create("Test Customer", ValidEmail, "hash");
        customer.MarkEmailVerified();
        return customer;
    }

    private void SetupCustomerLookup(CustomerAccount? customer)
    {
        _customerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<CustomerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
    }

    [Fact]
    public async Task Handle_EmailNotFound_ReturnsGenericSuccessToPreventEnumeration()
    {
        SetupCustomerLookup(null);

        var command = new ForgotPasswordCustomerCommand(ValidEmail);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmailNotFound_DoesNotStoreOtpInCache()
    {
        SetupCustomerLookup(null);

        var command = new ForgotPasswordCustomerCommand(ValidEmail);
        await _sut.Handle(command, CancellationToken.None);

        _cacheServiceMock.Verify(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmailNotFound_DoesNotSendEmail()
    {
        SetupCustomerLookup(null);

        var command = new ForgotPasswordCustomerCommand(ValidEmail);
        await _sut.Handle(command, CancellationToken.None);

        _emailServiceMock.Verify(x => x.SendEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AccountIsPendingDeletion_ReturnsGenericSuccessToPreventEnumeration()
    {
        var customer = CustomerAccount.Create("Test Customer", ValidEmail, "hash");
        var prop = typeof(CustomerAccount).GetProperty("Status")!;
        prop.SetValue(customer, CustomerStatus.PendingDeletion);
        SetupCustomerLookup(customer);

        var command = new ForgotPasswordCustomerCommand(ValidEmail);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AccountIsPendingDeletion_DoesNotSendEmail()
    {
        var customer = CustomerAccount.Create("Test Customer", ValidEmail, "hash");
        var prop = typeof(CustomerAccount).GetProperty("Status")!;
        prop.SetValue(customer, CustomerStatus.PendingDeletion);
        SetupCustomerLookup(customer);

        var command = new ForgotPasswordCustomerCommand(ValidEmail);
        await _sut.Handle(command, CancellationToken.None);

        _emailServiceMock.Verify(x => x.SendEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidActiveCustomer_StoresHashedOtpInCache()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);

        string? capturedCacheKey = null;
        string? capturedHashedOtp = null;
        TimeSpan? capturedTtl = null;

        _cacheServiceMock.Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Callback((string key, string val, TimeSpan? ttl, CancellationToken _) =>
            {
                capturedCacheKey = key;
                capturedHashedOtp = val;
                capturedTtl = ttl;
            })
            .Returns(Task.CompletedTask);

        var command = new ForgotPasswordCustomerCommand(ValidEmail);
        await _sut.Handle(command, CancellationToken.None);

        capturedCacheKey.Should().Be($"customer_pwd_reset:{ValidEmail}");
        capturedHashedOtp.Should().NotBeNullOrEmpty();
        capturedHashedOtp.Should().StartWith("$2");
        capturedTtl.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Handle_ValidActiveCustomer_CacheKeyUsesNormalizedEmail()
    {
        var customer = CustomerAccount.Create("Test Customer", "customer@example.com", "hash");
        customer.MarkEmailVerified();
        SetupCustomerLookup(customer);

        string? capturedKey = null;
        _cacheServiceMock.Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Callback((string key, string _, TimeSpan? __, CancellationToken ___) => capturedKey = key)
            .Returns(Task.CompletedTask);

        var command = new ForgotPasswordCustomerCommand("  CUSTOMER@EXAMPLE.COM  ");
        await _sut.Handle(command, CancellationToken.None);

        capturedKey.Should().Be("customer_pwd_reset:customer@example.com");
    }

    [Fact]
    public async Task Handle_ValidActiveCustomer_ReturnsSuccess()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);

        var command = new ForgotPasswordCustomerCommand(ValidEmail);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidActiveCustomer_ReturnsGenericSuccessMessage()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);

        var command = new ForgotPasswordCustomerCommand(ValidEmail);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Message.Should().Contain("If an account exists");
    }

    [Fact]
    public async Task Handle_SameResponseForExistingAndNonExistingEmails()
    {
        SetupCustomerLookup(null);
        var command1 = new ForgotPasswordCustomerCommand("notexist@example.com");
        var result1 = await _sut.Handle(command1, CancellationToken.None);

        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);
        var command2 = new ForgotPasswordCustomerCommand(ValidEmail);
        var result2 = await _sut.Handle(command2, CancellationToken.None);

        result1.IsSuccess.Should().Be(result2.IsSuccess);
        result1.Message.Should().Be(result2.Message);
    }

    [Fact]
    public async Task Handle_ValidActiveCustomer_OtpStoredAsHashNotPlaintext()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);

        string? storedValue = null;
        _cacheServiceMock.Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, string val, TimeSpan? __, CancellationToken ___) => storedValue = val)
            .Returns(Task.CompletedTask);

        var command = new ForgotPasswordCustomerCommand(ValidEmail);
        await _sut.Handle(command, CancellationToken.None);

        storedValue.Should().NotBeNull();
        storedValue!.Length.Should().BeGreaterThan(6);
        int.TryParse(storedValue, out _).Should().BeFalse();
    }
}