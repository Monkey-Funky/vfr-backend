using Application.Features.Auth.Commands.RefreshToken;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Tests.Unit.Application.Features.Auth.Retailer;

public sealed class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ILogger<RefreshTokenCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock = new();
    private readonly RefreshTokenCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private const string ExpiredAccessToken = "expired.access.token";
    private const string RawRefreshToken = "raw_refresh_token_value";
    private const string NewAccessToken = "new.access.token";
    private const string NewRefreshToken = "new_refresh_token_value";
    private static readonly string RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(RawRefreshToken, workFactor: 4);

    public RefreshTokenCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<RetailerAccount>()).Returns(_retailerRepoMock.Object);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<RetailerAccount>())).Returns(NewAccessToken);
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(NewRefreshToken);

        _sut = new RefreshTokenCommandHandler(_uowMock.Object, _tokenServiceMock.Object, _loggerMock.Object);
    }

    private ClaimsPrincipal BuildPrincipalWithSub(string sub) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, sub) }));

    private void SetupValidPrincipal() =>
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(ExpiredAccessToken))
            .Returns(BuildPrincipalWithSub(RetailerId.ToString()));

    private RetailerAccount CreateActiveRetailerWithRefreshToken(bool rememberMe = false)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("password", workFactor: 4);
        var account = RetailerAccount.Create("John Doe", "john@example.com", hash, "TestBrand");
        account.CompleteRegistration("Fashion", false, null);
        account.UpdateRefreshToken(RefreshTokenHash, DateTime.UtcNow.AddDays(7), rememberMe);
        return account;
    }

    private static RefreshTokenCommand ValidCommand() =>
        new(ExpiredAccessToken, RawRefreshToken);

    [Fact]
    public async Task Handle_InvalidAccessToken_ThrowsUnauthorizedAccessException()
    {
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(ExpiredAccessToken))
            .Returns((ClaimsPrincipal?)null);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*access token is invalid*");
    }

    [Fact]
    public async Task Handle_MissingSubClaim_ThrowsUnauthorizedAccessException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("email", "test@test.com") }));
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(ExpiredAccessToken)).Returns(principal);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*valid identity*");
    }

    [Fact]
    public async Task Handle_InvalidGuidSubClaim_ThrowsUnauthorizedAccessException()
    {
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(ExpiredAccessToken))
            .Returns(BuildPrincipalWithSub("not-a-guid"));

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*valid identity*");
    }

    [Fact]
    public async Task Handle_EmptyGuidSubClaim_ThrowsUnauthorizedAccessException()
    {
        _tokenServiceMock.Setup(x => x.GetClaimsFromExpiredToken(ExpiredAccessToken))
            .Returns(BuildPrincipalWithSub(Guid.Empty.ToString()));

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsUnauthorizedAccessException()
    {
        SetupValidPrincipal();
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount?)null);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Account not found*");
    }

    [Fact]
    public async Task Handle_AccountSoftDeleted_ThrowsUnauthorizedAccessException()
    {
        SetupValidPrincipal();
        var hash = BCrypt.Net.BCrypt.HashPassword("pass", workFactor: 4);
        var account = RetailerAccount.Create("Jane", "jane@example.com", hash, "Brand");
        account.MarkAsDeleted();
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_AccountNotActive_ThrowsUnauthorizedAccessException()
    {
        SetupValidPrincipal();
        var hash = BCrypt.Net.BCrypt.HashPassword("pass", workFactor: 4);
        var account = RetailerAccount.Create("Jane", "jane@example.com", hash, "Brand");
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*no longer active*");
    }

    [Fact]
    public async Task Handle_NoActiveRefreshToken_ThrowsUnauthorizedAccessException()
    {
        SetupValidPrincipal();
        var hash = BCrypt.Net.BCrypt.HashPassword("pass", workFactor: 4);
        var account = RetailerAccount.Create("Jane", "jane@example.com", hash, "Brand");
        account.CompleteRegistration("Fashion", false, null);
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*No active refresh token*");
    }

    [Fact]
    public async Task Handle_RefreshTokenHashMismatch_ThrowsUnauthorizedAccessException()
    {
        SetupValidPrincipal();
        var account = CreateActiveRetailerWithRefreshToken();
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var command = new RefreshTokenCommand(ExpiredAccessToken, "wrong_refresh_token");

        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*refresh token is invalid*");
    }

    [Fact]
    public async Task Handle_ExpiredRefreshToken_ThrowsUnauthorizedAccessException()
    {
        SetupValidPrincipal();
        var hash = BCrypt.Net.BCrypt.HashPassword("pass", workFactor: 4);
        var account = RetailerAccount.Create("Jane", "jane@example.com", hash, "Brand");
        account.CompleteRegistration("Fashion", false, null);
        account.UpdateRefreshToken(RefreshTokenHash, DateTime.UtcNow.AddDays(-1), false);
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task Handle_ConcurrentRotation_ThrowsUnauthorizedAccessException()
    {
        SetupValidPrincipal();
        var account = CreateActiveRetailerWithRefreshToken();
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*concurrent session refresh*");
    }

    [Fact]
    public async Task Handle_ValidRequest_RememberMeFalse_RotatesWithSevenDayExpiry()
    {
        SetupValidPrincipal();
        var account = CreateActiveRetailerWithRefreshToken(rememberMe: false);
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        account.RefreshTokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        account.IsRememberMeSession.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidRequest_RememberMeTrue_RotatesWithThirtyDayExpiry()
    {
        SetupValidPrincipal();
        var account = CreateActiveRetailerWithRefreshToken(rememberMe: true);
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        account.RefreshTokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsNewTokens()
    {
        SetupValidPrincipal();
        var account = CreateActiveRetailerWithRefreshToken();
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.Data!.AccessToken.Should().Be(NewAccessToken);
        result.Data.RefreshToken.Should().Be(NewRefreshToken);
        result.Data.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsRotatedToken()
    {
        SetupValidPrincipal();
        var account = CreateActiveRetailerWithRefreshToken();
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _retailerRepoMock.Verify(x => x.UpdateAsync(account, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_NewRefreshTokenHashDiffersFromOld()
    {
        SetupValidPrincipal();
        var account = CreateActiveRetailerWithRefreshToken();
        var originalHash = account.RefreshTokenHash;
        _retailerRepoMock.Setup(x => x.GetByIdAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        account.RefreshTokenHash.Should().NotBe(originalHash);
    }
}