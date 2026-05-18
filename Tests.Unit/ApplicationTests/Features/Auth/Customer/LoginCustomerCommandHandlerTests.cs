using Application.Features.Customer.Auth.Commands.Login;
using Application.Features.Customer.Auth.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Customer;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Customer.Auth;

public sealed class LoginCustomerCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ILogger<LoginCustomerCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<CustomerAccount>> _customerRepoMock = new();
    private readonly LoginCustomerCommandHandler _sut;

    private const string ValidEmail = "customer@example.com";
    private const string ValidPassword = "Password123!";
    private static readonly string ValidPasswordHash = BCrypt.Net.BCrypt.HashPassword(ValidPassword, workFactor: 4);

    public LoginCustomerCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<CustomerAccount>()).Returns(_customerRepoMock.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _customerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _tokenServiceMock.Setup(x => x.GenerateCustomerAccessToken(It.IsAny<CustomerAccount>())).Returns("access-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("raw-refresh-token");

        _sut = new LoginCustomerCommandHandler(
            _uowMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    private CustomerAccount CreateActiveCustomer(string? passwordHash = null)
    {
        var customer = CustomerAccount.Create("Test Customer", ValidEmail, passwordHash ?? ValidPasswordHash);
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

    private static CustomerAccount CreateCustomerWithStatus(string status)
    {
        var customer = CustomerAccount.Create("Test Customer", ValidEmail, ValidPasswordHash);
        var prop = typeof(CustomerAccount).GetProperty("Status")!;
        prop.SetValue(customer, status);
        return customer;
    }

    [Fact]
    public async Task Handle_CustomerNotFound_ThrowsAuthenticationException()
    {
        SetupCustomerLookup(null);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword);

        await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CustomerNotFound_ThrowsWithGenericMessage()
    {
        SetupCustomerLookup(null);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Handle_AccountLockedOut_ThrowsBusinessRuleException()
    {
        var customer = CreateActiveCustomer();
        for (var i = 0; i < 10; i++) customer.IncrementFailedLogin();

        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_LOCKED");
    }

    [Fact]
    public async Task Handle_GoogleOnlyAccount_PasswordHashIsNull_ThrowsAuthenticationException()
    {
        var customer = CustomerAccount.CreateWithGoogle("Test Customer", ValidEmail, "google-id-123");
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Handle_WrongPassword_IncrementsFailedLoginCount()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, "WrongPassword!");

        await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));

        customer.FailedLoginAttempts.Should().Be(1);
        _customerRepoMock.Verify(x => x.UpdateAsync(customer, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WrongPassword_ThrowsAuthenticationException()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, "WrongPassword!");

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Handle_TenthFailedLogin_LocksAccountAndThrowsBusinessRuleException()
    {
        var customer = CreateActiveCustomer();
        for (var i = 0; i < 9; i++) customer.IncrementFailedLogin();
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, "WrongPassword!");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_LOCKED");
        customer.IsLockedOut().Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PendingEmailVerificationStatus_ThrowsBusinessRuleException()
    {
        var customer = CustomerAccount.Create("Test Customer", ValidEmail, ValidPasswordHash);
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("EMAIL_NOT_VERIFIED");
    }

    [Fact]
    public async Task Handle_SuspendedAccount_ThrowsBusinessRuleException()
    {
        var customer = CreateCustomerWithStatus(CustomerStatus.Suspended);
        var phProp = typeof(CustomerAccount).GetProperty("PasswordHash")!;
        phProp.SetValue(customer, ValidPasswordHash);
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task Handle_PendingDeletionAccount_ThrowsBusinessRuleException()
    {
        var customer = CreateCustomerWithStatus(CustomerStatus.PendingDeletion);
        var phProp = typeof(CustomerAccount).GetProperty("PasswordHash")!;
        phProp.SetValue(customer, ValidPasswordHash);
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task Handle_ValidCredentials_ResetsFailedLoginCount()
    {
        var customer = CreateActiveCustomer();
        customer.IncrementFailedLogin();
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword);
        await _sut.Handle(command, CancellationToken.None);

        customer.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsAccessAndRefreshTokens()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("access-token");
        result.Data.RefreshToken.Should().Be("raw-refresh-token");
        result.Data.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Handle_ValidCredentials_WithoutRememberMe_Sets7DayRefreshToken()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword, RememberMe: false);
        await _sut.Handle(command, CancellationToken.None);

        customer.RefreshTokenExpiresAt.Should().NotBeNull();
        customer.RefreshTokenExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        customer.RememberMe.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidCredentials_WithRememberMe_Sets30DayRefreshToken()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword, RememberMe: true);
        await _sut.Handle(command, CancellationToken.None);

        customer.RefreshTokenExpiresAt.Should().NotBeNull();
        customer.RefreshTokenExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
        customer.RememberMe.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidCredentials_RefreshTokenHashIsNotRawToken()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword);
        await _sut.Handle(command, CancellationToken.None);

        customer.RefreshTokenHash.Should().NotBeNull();
        customer.RefreshTokenHash.Should().NotBe("raw-refresh-token");
    }

    [Fact]
    public async Task Handle_ValidCredentials_PersistsUpdatesToDatabase()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword);
        await _sut.Handle(command, CancellationToken.None);

        _customerRepoMock.Verify(x => x.UpdateAsync(customer, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCredentials_EmailLookupNormalizesEmail()
    {
        SetupCustomerLookup(null);

        var command = new LoginCustomerCommand("  CUSTOMER@EXAMPLE.COM  ", ValidPassword);
        await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));

        _customerRepoMock.Verify(x => x.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<CustomerAccount, bool>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsCustomerProfileInResponse()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);

        var command = new LoginCustomerCommand(ValidEmail, ValidPassword);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Data!.CustomerProfile.Should().NotBeNull();
        result.Data.CustomerProfile.Email.Should().Be(ValidEmail);
    }
}