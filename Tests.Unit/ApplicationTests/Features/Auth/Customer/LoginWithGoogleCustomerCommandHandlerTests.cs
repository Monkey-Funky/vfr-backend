using Application.Features.Customer.Auth.Commands.LoginWithGoogle;
using Application.Features.Customer.Auth.DTOs;
using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Customer.Auth;

public sealed class LoginWithGoogleCustomerCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IGoogleAuthService> _googleAuthServiceMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ILogger<LoginWithGoogleCustomerCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<CustomerAccount>> _customerRepoMock = new();
    private readonly LoginWithGoogleCustomerCommandHandler _sut;

    private static readonly GoogleUserInfo ValidGoogleUser = new(
        GoogleId: "google-customer-sub-789",
        Email: "customer@example.com",
        FullName: "Test Customer",
        IsEmailVerified: true);

    public LoginWithGoogleCustomerCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<CustomerAccount>()).Returns(_customerRepoMock.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _uowMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> op, CancellationToken ct) => op(ct));

        _customerRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAccount c, CancellationToken _) => c);
        _customerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _tokenServiceMock.Setup(x => x.GenerateCustomerAccessToken(It.IsAny<CustomerAccount>()))
            .Returns("access-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("raw-refresh-token");

        _sut = new LoginWithGoogleCustomerCommandHandler(
            _uowMock.Object,
            _googleAuthServiceMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    private void SetupGoogleValidation(GoogleUserInfo? user = null)
    {
        _googleAuthServiceMock.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user ?? ValidGoogleUser);
    }

    private void SetupCustomerLookup(CustomerAccount? customer)
    {
        _customerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<CustomerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
    }

    private static CustomerAccount CreateActiveCustomerWithGoogle(string googleId = "google-customer-sub-789")
        => CustomerAccount.CreateWithGoogle("Test Customer", "customer@example.com", googleId);

    private static CustomerAccount CreatePendingCustomer()
        => CustomerAccount.Create("Test Customer", "customer@example.com", "hash");

    private static CustomerAccount CreateCustomerWithStatus(string status)
    {
        var customer = CustomerAccount.CreateWithGoogle("Test Customer", "customer@example.com", "gid");
        var prop = typeof(CustomerAccount).GetProperty("Status")!;
        prop.SetValue(customer, status);
        return customer;
    }

    [Fact]
    public async Task Handle_InvalidGoogleToken_PropagatesExternalServiceException()
    {
        _googleAuthServiceMock.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalServiceException("Google", "Invalid token"));

        var command = new LoginWithGoogleCustomerCommand("bad-token");

        await Assert.ThrowsAsync<ExternalServiceException>(() => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NewAccount_CreatesCustomerInTransaction()
    {
        SetupGoogleValidation();
        SetupCustomerLookup(null);

        var command = new LoginWithGoogleCustomerCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _customerRepoMock.Verify(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NewAccount_ReturnsAccessAndRefreshTokens()
    {
        SetupGoogleValidation();
        SetupCustomerLookup(null);

        var command = new LoginWithGoogleCustomerCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("access-token");
        result.Data.RefreshToken.Should().Be("raw-refresh-token");
    }

    [Fact]
    public async Task Handle_NewAccount_CreatedCustomerIsActiveAndEmailVerified()
    {
        SetupGoogleValidation();
        SetupCustomerLookup(null);

        CustomerAccount? capturedCustomer = null;
        _customerRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Callback((CustomerAccount c, CancellationToken _) => capturedCustomer = c)
            .ReturnsAsync((CustomerAccount c, CancellationToken _) => c);

        var command = new LoginWithGoogleCustomerCommand("valid-token");
        await _sut.Handle(command, CancellationToken.None);

        capturedCustomer.Should().NotBeNull();
        capturedCustomer!.Status.Should().Be(CustomerStatus.Active);
        capturedCustomer.IsEmailVerified.Should().BeTrue();
        capturedCustomer.GoogleId.Should().Be(ValidGoogleUser.GoogleId);
    }

    [Fact]
    public async Task Handle_ExistingActiveCustomerWithGoogleId_UpdatesRefreshTokenOnly()
    {
        var customer = CreateActiveCustomerWithGoogle();
        SetupGoogleValidation();
        SetupCustomerLookup(customer);

        var command = new LoginWithGoogleCustomerCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _customerRepoMock.Verify(x => x.UpdateAsync(customer, It.IsAny<CancellationToken>()), Times.Once);
        _customerRepoMock.Verify(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(x => x.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExistingActiveEmailCustomerWithoutGoogleId_LinksGoogleIdAndReturnsTokens()
    {
        var customer = CreatePendingCustomer();
        customer.MarkEmailVerified();
        SetupGoogleValidation();
        SetupCustomerLookup(customer);

        var command = new LoginWithGoogleCustomerCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        customer.GoogleId.Should().Be(ValidGoogleUser.GoogleId);
        _customerRepoMock.Verify(x => x.UpdateAsync(customer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingPendingEmailVerificationCustomerWithoutGoogleId_LinksAndActivates()
    {
        var customer = CreatePendingCustomer();
        SetupGoogleValidation();
        SetupCustomerLookup(customer);

        var command = new LoginWithGoogleCustomerCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        customer.GoogleId.Should().Be(ValidGoogleUser.GoogleId);
        customer.Status.Should().Be(CustomerStatus.Active);
        customer.IsEmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SuspendedAccount_ThrowsBusinessRuleException()
    {
        var customer = CreateCustomerWithStatus(CustomerStatus.Suspended);
        SetupGoogleValidation();
        SetupCustomerLookup(customer);

        var command = new LoginWithGoogleCustomerCommand("valid-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task Handle_PendingDeletionAccount_ThrowsBusinessRuleException()
    {
        var customer = CreateCustomerWithStatus(CustomerStatus.PendingDeletion);
        SetupGoogleValidation();
        SetupCustomerLookup(customer);

        var command = new LoginWithGoogleCustomerCommand("valid-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task Handle_SuspendedAccount_ErrorMessageContainsStatus()
    {
        var customer = CreateCustomerWithStatus(CustomerStatus.Suspended);
        SetupGoogleValidation();
        SetupCustomerLookup(customer);

        var command = new LoginWithGoogleCustomerCommand("valid-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().Contain("suspended");
    }

    [Fact]
    public async Task Handle_NewAccount_UniqueConstraintViolation_ThrowsBusinessRuleException()
    {
        SetupGoogleValidation();
        SetupCustomerLookup(null);

        var innerEx = new Exception("duplicate key value violates unique constraint");
        var dbEx = new DbUpdateException("Unique violation", innerEx);

        _uowMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(dbEx);

        var command = new LoginWithGoogleCustomerCommand("valid-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_CONFLICT");
    }

    [Fact]
    public async Task Handle_ExistingAccount_UniqueConstraintViolationOnUpdate_ThrowsBusinessRuleException()
    {
        var customer = CreateActiveCustomerWithGoogle();
        SetupGoogleValidation();
        SetupCustomerLookup(customer);

        var innerEx = new Exception("unique constraint violation");
        var dbEx = new DbUpdateException("Unique violation", innerEx);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(dbEx);

        var command = new LoginWithGoogleCustomerCommand("valid-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_CONFLICT");
    }

    [Fact]
    public async Task Handle_ValidLogin_RefreshTokenStoredAsHash()
    {
        SetupGoogleValidation();
        SetupCustomerLookup(null);

        CustomerAccount? capturedCustomer = null;
        _customerRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Callback((CustomerAccount c, CancellationToken _) => capturedCustomer = c)
            .ReturnsAsync((CustomerAccount c, CancellationToken _) => c);

        var command = new LoginWithGoogleCustomerCommand("valid-token");
        await _sut.Handle(command, CancellationToken.None);

        capturedCustomer!.RefreshTokenHash.Should().NotBeNull();
        capturedCustomer.RefreshTokenHash.Should().NotBe("raw-refresh-token");
    }

    [Fact]
    public async Task Handle_ValidLogin_RefreshTokenExpiry7Days()
    {
        SetupGoogleValidation();
        SetupCustomerLookup(null);

        CustomerAccount? capturedCustomer = null;
        _customerRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Callback((CustomerAccount c, CancellationToken _) => capturedCustomer = c)
            .ReturnsAsync((CustomerAccount c, CancellationToken _) => c);

        var command = new LoginWithGoogleCustomerCommand("valid-token");
        await _sut.Handle(command, CancellationToken.None);

        capturedCustomer!.RefreshTokenExpiresAt.Should().NotBeNull();
        capturedCustomer.RefreshTokenExpiresAt!.Value
            .Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ValidLogin_ReturnsSuccessResult()
    {
        SetupGoogleValidation();
        SetupCustomerLookup(null);

        var command = new LoginWithGoogleCustomerCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_ValidLogin_ReturnsCustomerProfileInResponse()
    {
        var customer = CreateActiveCustomerWithGoogle();
        SetupGoogleValidation();
        SetupCustomerLookup(customer);

        var command = new LoginWithGoogleCustomerCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Data!.CustomerProfile.Should().NotBeNull();
        result.Data.CustomerProfile.Email.Should().Be("customer@example.com");
    }
}