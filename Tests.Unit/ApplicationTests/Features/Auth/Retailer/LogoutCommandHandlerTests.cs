using Application.Features.Auth.Commands.Logout;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Tests.Unit.Application.Features.Auth.Retailer;

public sealed class LogoutCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ILogger<LogoutCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock = new();
    private readonly LogoutCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private const string BearerToken = "some.bearer.token";

    public LogoutCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<RetailerAccount>()).Returns(_retailerRepoMock.Object);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _sut = new LogoutCommandHandler(_uowMock.Object, _currentUserServiceMock.Object, _tokenServiceMock.Object, _loggerMock.Object);
    }

    private RetailerAccount CreateActiveRetailerWithToken()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("pass", workFactor: 4);
        var account = RetailerAccount.Create("John Doe", "john@example.com", hash, "Brand");
        account.CompleteRegistration("Fashion", false, null);
        var tokenHash = BCrypt.Net.BCrypt.HashPassword("sometoken", workFactor: 4);
        account.UpdateRefreshToken(tokenHash, DateTime.UtcNow.AddDays(7), false);
        return account;
    }

    [Fact]
    public async Task Handle_NoRetailerIdAndNoBearer_ReturnsIdempotentSuccess()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        _currentUserServiceMock.Setup(x => x.GetRawBearerToken()).Returns((string?)null);

        var result = await _sut.Handle(new LogoutCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        _retailerRepoMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoRetailerIdAndUnverifiableBearer_ReturnsIdempotentSuccess()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        _currentUserServiceMock.Setup(x => x.GetRawBearerToken()).Returns(BearerToken);
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(BearerToken)).Returns((ClaimsPrincipal?)null);

        var result = await _sut.Handle(new LogoutCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _retailerRepoMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoRetailerIdButValidExpiredToken_RevokesRefreshToken()
    {
        var account = CreateActiveRetailerWithToken();
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        _currentUserServiceMock.Setup(x => x.GetRawBearerToken()).Returns(BearerToken);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, RetailerId.ToString()) }));
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(BearerToken)).Returns(principal);
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var result = await _sut.Handle(new LogoutCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        account.RefreshTokenHash.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidRetailerId_AccountNotFound_ReturnsIdempotentSuccess()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount?)null);

        var result = await _sut.Handle(new LogoutCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidRetailerId_AccountSoftDeleted_ReturnsIdempotentSuccess()
    {
        var account = CreateActiveRetailerWithToken();
        account.MarkAsDeleted();
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.Handle(new LogoutCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidRetailerId_RevokesRefreshToken()
    {
        var account = CreateActiveRetailerWithToken();
        account.RefreshTokenHash.Should().NotBeNull();

        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.Handle(new LogoutCommand(), CancellationToken.None);

        account.RefreshTokenHash.Should().BeNull();
        account.RefreshTokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidRetailerId_PersistsRevocation()
    {
        var account = CreateActiveRetailerWithToken();
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.Handle(new LogoutCommand(), CancellationToken.None);

        _retailerRepoMock.Verify(x => x.UpdateAsync(account, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRetailerId_ReturnsSuccess()
    {
        var account = CreateActiveRetailerWithToken();
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.Handle(new LogoutCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ExpiredTokenWithInvalidSubClaim_ReturnsIdempotentSuccess()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        _currentUserServiceMock.Setup(x => x.GetRawBearerToken()).Returns(BearerToken);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") }));
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(BearerToken)).Returns(principal);

        var result = await _sut.Handle(new LogoutCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _retailerRepoMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}