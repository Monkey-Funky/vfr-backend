using Application.Features.Customer.Auth.Commands.CompleteProfile;
using Application.Features.Customer.Auth.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Customer;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Tests.Unit.Application.Features.Customer.Auth;

public sealed class CompleteCustomerProfileCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<CompleteCustomerProfileCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<CustomerAccount>> _customerRepoMock = new();
    private readonly CompleteCustomerProfileCommandHandler _sut;

    private static readonly Guid AccountId = Guid.NewGuid();
    private const string ValidGender = "Male";
    private static readonly DateOnly ValidDateOfBirth = new(1990, 6, 15);
    private const string ValidPhone = "+1234567890";
    private const string ValidStepToken = "valid-step-token";

    public CompleteCustomerProfileCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<CustomerAccount>()).Returns(_customerRepoMock.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _uowMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> op, CancellationToken ct) => op(ct));

        _customerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _emailServiceMock.Setup(x => x.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _tokenServiceMock.Setup(x => x.GenerateCustomerAccessToken(It.IsAny<CustomerAccount>()))
            .Returns("access-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("raw-refresh-token");

        _sut = new CompleteCustomerProfileCommandHandler(
            _uowMock.Object,
            _tokenServiceMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    private ClaimsPrincipal BuildValidStepPrincipal(
        string tokenType = "step",
        string step = "1",
        Guid? accountId = null)
    {
        var claims = new List<Claim>
        {
            new("token_type", tokenType),
            new("step", step),
            new("temp_account_id", (accountId ?? AccountId).ToString())
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    private void SetupValidToken(ClaimsPrincipal? principal = null)
    {
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(principal ?? BuildValidStepPrincipal());
    }

    private void SetupAccountById(CustomerAccount? account)
    {
        _customerRepoMock.Setup(x => x.GetByIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
    }

    private CustomerAccount CreatePendingCustomer()
    {
        var customer = CustomerAccount.Create("Test Customer", "customer@example.com", "hash");
        var idProp = typeof(CustomerAccount).GetProperty("Id")!;
        idProp.SetValue(customer, AccountId);
        return customer;
    }

    [Fact]
    public async Task Handle_NullPrincipalFromTokenValidation_ThrowsAuthenticationException()
    {
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns((ClaimsPrincipal?)null);

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task Handle_WrongTokenType_ThrowsAuthenticationException()
    {
        SetupValidToken(BuildValidStepPrincipal(tokenType: "access"));

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_WrongStepNumber_ThrowsAuthenticationException()
    {
        SetupValidToken(BuildValidStepPrincipal(step: "2"));

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_MissingTokenTypeClaim_ThrowsAuthenticationException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("step", "1"),
            new Claim("temp_account_id", AccountId.ToString())
        }));
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>())).Returns(principal);

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);

        await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsNotFoundException()
    {
        SetupValidToken();
        SetupAccountById(null);

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AccountAlreadyActive_ThrowsBusinessRuleException()
    {
        SetupValidToken();
        var customer = CreatePendingCustomer();
        customer.MarkEmailVerified();
        SetupAccountById(customer);

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("REGISTRATION_ALREADY_COMPLETED");
    }

    [Fact]
    public async Task Handle_ValidToken_SetsGenderOnAccount()
    {
        SetupValidToken();
        var customer = CreatePendingCustomer();
        SetupAccountById(customer);

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);
        await _sut.Handle(command, CancellationToken.None);

        customer.Gender.Should().Be(ValidGender);
    }

    [Fact]
    public async Task Handle_ValidToken_SetsDateOfBirthOnAccount()
    {
        SetupValidToken();
        var customer = CreatePendingCustomer();
        SetupAccountById(customer);

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);
        await _sut.Handle(command, CancellationToken.None);

        customer.DateOfBirth.Should().Be(ValidDateOfBirth);
    }

    [Fact]
    public async Task Handle_ValidToken_SetsPhoneNumberOnAccount()
    {
        SetupValidToken();
        var customer = CreatePendingCustomer();
        SetupAccountById(customer);

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);
        await _sut.Handle(command, CancellationToken.None);

        customer.PhoneNumber.Should().Be(ValidPhone);
    }

    [Fact]
    public async Task Handle_ValidToken_TransitionsAccountStatusToActive()
    {
        SetupValidToken();
        var customer = CreatePendingCustomer();
        SetupAccountById(customer);

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);
        await _sut.Handle(command, CancellationToken.None);

        customer.Status.Should().Be(CustomerStatus.Active);
        customer.IsEmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsAccessAndRefreshTokens()
    {
        SetupValidToken();
        SetupAccountById(CreatePendingCustomer());

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("access-token");
        result.Data.RefreshToken.Should().Be("raw-refresh-token");
    }

    [Fact]
    public async Task Handle_ValidToken_RefreshTokenExpiresIn7Days()
    {
        SetupValidToken();
        var customer = CreatePendingCustomer();
        SetupAccountById(customer);

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);
        await _sut.Handle(command, CancellationToken.None);

        customer.RefreshTokenExpiresAt.Should().NotBeNull();
        customer.RefreshTokenExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ValidToken_PersistsChangesToDatabase()
    {
        SetupValidToken();
        var customer = CreatePendingCustomer();
        SetupAccountById(customer);

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);
        await _sut.Handle(command, CancellationToken.None);

        _customerRepoMock.Verify(x => x.UpdateAsync(customer, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsCustomerProfileWithCorrectEmail()
    {
        SetupValidToken();
        SetupAccountById(CreatePendingCustomer());

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Data!.CustomerProfile.Email.Should().Be("customer@example.com");
    }

    [Fact]
    public async Task Handle_ValidTokenWithNullPhone_CompletesSuccessfully()
    {
        SetupValidToken();
        var customer = CreatePendingCustomer();
        SetupAccountById(customer);

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, null, ValidStepToken);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        customer.PhoneNumber.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidToken_RefreshTokenStoredAsHash()
    {
        SetupValidToken();
        var customer = CreatePendingCustomer();
        SetupAccountById(customer);

        var command = new CompleteCustomerProfileCommand(ValidGender, ValidDateOfBirth, ValidPhone, ValidStepToken);
        await _sut.Handle(command, CancellationToken.None);

        customer.RefreshTokenHash.Should().NotBeNull();
        customer.RefreshTokenHash.Should().NotBe("raw-refresh-token");
    }
}