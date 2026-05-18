using Application.Features.Customer.Auth.Commands.RefreshToken;
using Application.Features.Customer.Auth.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Customer;
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
    private const string ValidRawRefreshToken = "valid-raw-refresh-token";
    private static readonly string ValidRefreshTokenHash =
        BCrypt.Net.BCrypt.EnhancedHashPassword(ValidRawRefreshToken, workFactor: 4);

    public RefreshCustomerTokenCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<CustomerAccount>()).Returns(_customerRepoMock.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _customerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _tokenServiceMock.Setup(x => x.GenerateCustomerAccessToken(It.IsAny<CustomerAccount>()))
            .Returns("new-access-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("new-raw-refresh-token");

        _sut = new RefreshCustomerTokenCommandHandler(
            _uowMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    private ClaimsPrincipal BuildValidPrincipal(Guid? subId = null, string role = "Customer")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, (subId ?? CustomerId).ToString()),
            new("role", role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    private CustomerAccount CreateActiveCustomer(bool rememberMe = false)
    {
        var customer = CustomerAccount.Create("Test Customer", "customer@example.com",
            BCrypt.Net.BCrypt.HashPassword("pass", workFactor: 4));
        customer.MarkEmailVerified();
        customer.UpdateRefreshToken(ValidRefreshTokenHash, DateTime.UtcNow.AddDays(7), rememberMe);
        return customer;
    }

    private void SetupValidPrincipal(Guid? subId = null, string role = "Customer")
    {
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(subId, role));
    }

    private void SetupCustomerById(CustomerAccount? customer)
    {
        _customerRepoMock.Setup(x => x.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
    }

    [Fact]
    public async Task Handle_InvalidAccessTokenStructure_ThrowsAuthenticationException()
    {
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(It.IsAny<string>()))
            .Returns((ClaimsPrincipal?)null);

        var command = new RefreshCustomerTokenCommand("bad-token", ValidRawRefreshToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().Contain("invalid");
    }

    [Fact]
    public async Task Handle_MissingSubClaim_ThrowsAuthenticationException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("role", "Customer") }));
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(It.IsAny<string>())).Returns(principal);

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_InvalidSubClaimFormat_ThrowsAuthenticationException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "not-a-guid"),
            new Claim("role", "Customer")
        }));
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(It.IsAny<string>())).Returns(principal);

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_EmptyGuidSubClaim_ThrowsAuthenticationException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()),
            new Claim("role", "Customer")
        }));
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(It.IsAny<string>())).Returns(principal);

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_RetailerRoleInToken_ThrowsAuthenticationException()
    {
        SetupValidPrincipal(role: "Retailer");

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().Contain("Customer");
    }

    [Fact]
    public async Task Handle_UnknownRoleInToken_ThrowsAuthenticationException()
    {
        SetupValidPrincipal(role: "Admin");

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_CustomerNotFound_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        SetupCustomerById(null);

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_DeletedCustomer_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomer();
        var deletedProp = typeof(CustomerAccount).GetProperty("IsDeleted",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!;
        deletedProp.SetValue(customer, true);
        SetupCustomerById(customer);

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_SuspendedCustomer_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomer();
        var statusProp = typeof(CustomerAccount).GetProperty("Status")!;
        statusProp.SetValue(customer, CustomerStatus.Suspended);
        SetupCustomerById(customer);

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_NoRefreshTokenOnAccount_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        var customer = CustomerAccount.Create("Test Customer", "customer@example.com", "hash");
        customer.MarkEmailVerified();
        SetupCustomerById(customer);

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_RefreshTokenHashMismatch_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomer();
        var wrongHash = BCrypt.Net.BCrypt.EnhancedHashPassword("different-token", workFactor: 4);
        customer.UpdateRefreshToken(wrongHash, DateTime.UtcNow.AddDays(7), false);
        SetupCustomerById(customer);

        var command = new RefreshCustomerTokenCommand("access-token", "wrong-raw-token");

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().Contain("invalid");
    }

    [Fact]
    public async Task Handle_RefreshTokenExpired_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomer();
        customer.UpdateRefreshToken(ValidRefreshTokenHash, DateTime.UtcNow.AddDays(-1), false);
        SetupCustomerById(customer);

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task Handle_ConcurrentRotationDetected_ThrowsAuthenticationException()
    {
        SetupValidPrincipal();
        SetupCustomerById(CreateActiveCustomer());
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().Contain("concurrent");
    }

    [Fact]
    public async Task Handle_ValidTokens_ReturnsNewAccessAndRefreshTokens()
    {
        SetupValidPrincipal();
        SetupCustomerById(CreateActiveCustomer());

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("new-access-token");
        result.Data.RefreshToken.Should().Be("new-raw-refresh-token");
    }

    [Fact]
    public async Task Handle_ValidTokens_WithoutRememberMe_Preserves7DayExpiry()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomer(rememberMe: false);
        SetupCustomerById(customer);

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);
        await _sut.Handle(command, CancellationToken.None);

        customer.RefreshTokenExpiresAt.Should().NotBeNull();
        customer.RefreshTokenExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ValidTokens_WithRememberMe_Preserves30DayExpiry()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomer(rememberMe: true);
        SetupCustomerById(customer);

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);
        await _sut.Handle(command, CancellationToken.None);

        customer.RefreshTokenExpiresAt.Should().NotBeNull();
        customer.RefreshTokenExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ValidTokens_NewRefreshTokenHashDiffersFromRaw()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomer();
        SetupCustomerById(customer);

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);
        await _sut.Handle(command, CancellationToken.None);

        customer.RefreshTokenHash.Should().NotBe("new-raw-refresh-token");
    }

    [Fact]
    public async Task Handle_ValidTokens_PersistsUpdatedCustomer()
    {
        SetupValidPrincipal();
        var customer = CreateActiveCustomer();
        SetupCustomerById(customer);

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);
        await _sut.Handle(command, CancellationToken.None);

        _customerRepoMock.Verify(x => x.UpdateAsync(customer, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidTokens_ReturnsCustomerProfileInResponse()
    {
        SetupValidPrincipal();
        SetupCustomerById(CreateActiveCustomer());

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Data!.CustomerProfile.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ValidTokens_ExpiresInIs900Seconds()
    {
        SetupValidPrincipal();
        SetupCustomerById(CreateActiveCustomer());

        var command = new RefreshCustomerTokenCommand("access-token", ValidRawRefreshToken);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.Data!.ExpiresIn.Should().Be(900);
    }
}