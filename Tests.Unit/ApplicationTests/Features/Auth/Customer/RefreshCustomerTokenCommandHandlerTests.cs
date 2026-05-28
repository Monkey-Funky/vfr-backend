using Application.Features.Customer.Auth.Commands.RefreshToken;
using Application.Features.Customer.Auth.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Tests.Unit.Application.Features.Customer.Auth;

public sealed class RefreshCustomerTokenCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ILogger<RefreshCustomerTokenCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<CustomerAccount>> _customerRepoMock = new();
    private readonly RefreshCustomerTokenCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private const string ExpiredAccessToken = "expired.customer.access.token";
    private const string RawRefreshToken = "raw_customer_refresh_token";
    private const string NewAccessToken = "new.customer.access.token";
    private const string NewRefreshToken = "new_customer_refresh_token";
    private static readonly string RefreshTokenHash =
        BCrypt.Net.BCrypt.HashPassword(RawRefreshToken, workFactor: 4);

    public RefreshCustomerTokenCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<CustomerAccount>()).Returns(_customerRepoMock.Object);
        _customerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _tokenServiceMock.Setup(x => x.GenerateCustomerAccessToken(It.IsAny<CustomerAccount>()))
            .Returns(NewAccessToken);
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(NewRefreshToken);

        _sut = new RefreshCustomerTokenCommandHandler(
            _uowMock.Object, _tokenServiceMock.Object, _loggerMock.Object);
    }

    private ClaimsPrincipal BuildCustomerPrincipal(string sub) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, sub),
            new Claim("role", "Customer")
        }));

    private void SetupValidPrincipal() =>
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(ExpiredAccessToken))
            .Returns(BuildCustomerPrincipal(CustomerId.ToString()));

    private CustomerAccount CreateActiveCustomerWithRefreshToken(bool rememberMe = false)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 4);
        var customer = CustomerAccount.Create("Test Customer", "customer@example.com", hash);
        customer.MarkEmailVerified();
        customer.UpdateRefreshToken(RefreshTokenHash, DateTime.UtcNow.AddDays(7), rememberMe);
        return customer;
    }

    private static RefreshCustomerTokenCommand ValidCommand() =>
        new(ExpiredAccessToken, RawRefreshToken);

    // ?? Invalid access token ???????????????????????????????????????????????????

    [Fact]
    public async Task Handle_NullPrincipal_ThrowsAuthenticationException()
    {
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(ExpiredAccessToken))
            .Returns((ClaimsPrincipal?)null);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => _sut.Handle(ValidCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NullPrincipal_MessageIndicatesInvalidToken()
    {
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(ExpiredAccessToken))
            .Returns((ClaimsPrincipal?)null);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(
            () => _sut.Handle(ValidCommand(), CancellationToken.None));

        ex.Message.Should().Contain("invalid");
    }

    // ?? Missing / invalid sub claim ????????????????????????????????????????????

    [Fact]
    public async Task Handle_MissingSubClaim_ThrowsAuthenticationException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("email", "test@test.com"),
            new Claim("role", "Customer")
        }));
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(ExpiredAccessToken)).Returns(principal);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => _sut.Handle(ValidCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InvalidGuidSubClaim_ThrowsAuthenticationException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "not-a-guid"),
            new Claim("role", "Customer")
        }));
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(ExpiredAccessToken)).Returns(principal);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => _sut.Handle(ValidCommand(), CancellationToken.None));
    }

    // ?? Cross-role attack prevention ???????????????????????????????????????????

    [Fact]
    public async Task Handle_RetailerRoleClaim_ThrowsAuthenticationException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, CustomerId.ToString()),
            new Claim("role", "Retailer")   // wrong role
        }));
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(ExpiredAccessToken)).Returns(principal);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => _sut.Handle(ValidCommand(), CancellationToken.None));
    }

    // ?? Account not found / deleted ????????????????????????????????????????????

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAccount?)null);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => _sut.Handle(ValidCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DeletedAccount_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomerWithRefreshToken();
        typeof(CustomerAccount).GetProperty("IsDeleted")!.SetValue(customer, true);

        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => _sut.Handle(ValidCommand(), CancellationToken.None));
    }

    // ?? Inactive account ???????????????????????????????????????????????????????

    [Fact]
    public async Task Handle_SuspendedAccount_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        var hash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 4);
        var customer = CustomerAccount.Create("Test Customer", "customer@example.com", hash);
        // Not calling MarkEmailVerified ? status stays PendingEmailVerification (not Active)
        customer.UpdateRefreshToken(RefreshTokenHash, DateTime.UtcNow.AddDays(7), false);

        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => _sut.Handle(ValidCommand(), CancellationToken.None));
    }

    // ?? No active refresh token ????????????????????????????????????????????????

    [Fact]
    public async Task Handle_NullRefreshTokenHash_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomerWithRefreshToken();
        customer.RevokeAllRefreshTokens();

        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => _sut.Handle(ValidCommand(), CancellationToken.None));
    }

    // ?? Token hash mismatch ????????????????????????????????????????????????????

    [Fact]
    public async Task Handle_WrongRefreshToken_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomerWithRefreshToken();

        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        var command = new RefreshCustomerTokenCommand(ExpiredAccessToken, "wrong_token");

        await Assert.ThrowsAsync<AuthenticationException>(
            () => _sut.Handle(command, CancellationToken.None));
    }

    // ?? Expired refresh token ??????????????????????????????????????????????????

    [Fact]
    public async Task Handle_ExpiredRefreshToken_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomerWithRefreshToken();
        customer.UpdateRefreshToken(RefreshTokenHash, DateTime.UtcNow.AddDays(-1), false); // expired

        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => _sut.Handle(ValidCommand(), CancellationToken.None));
    }

    // ?? Successful rotation ????????????????????????????????????????????????????

    [Fact]
    public async Task Handle_ValidTokens_ReturnsNewAccessToken()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomerWithRefreshToken();
        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.AccessToken.Should().Be(NewAccessToken);
    }

    [Fact]
    public async Task Handle_ValidTokens_ReturnsNewRefreshToken()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomerWithRefreshToken();
        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.Data!.RefreshToken.Should().Be(NewRefreshToken);
    }

    [Fact]
    public async Task Handle_ValidTokens_HashesNewRefreshToken()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomerWithRefreshToken();
        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        customer.RefreshTokenHash.Should().NotBeNullOrEmpty();
        customer.RefreshTokenHash.Should().NotBe(NewRefreshToken);
    }

    [Fact]
    public async Task Handle_RememberMe_Preserves30DayExpiry()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomerWithRefreshToken(rememberMe: true);
        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        customer.RefreshTokenExpiresAt.Should()
            .BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_NoRememberMe_Preserves7DayExpiry()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomerWithRefreshToken(rememberMe: false);
        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        customer.RefreshTokenExpiresAt.Should()
            .BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ValidTokens_PersistsChanges()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomerWithRefreshToken();
        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _customerRepoMock.Verify(x => x.UpdateAsync(customer, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}