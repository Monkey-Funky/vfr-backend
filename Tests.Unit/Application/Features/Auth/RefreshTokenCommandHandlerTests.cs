// tests/Tests.Unit/Application/Features/Auth/RefreshTokenCommandHandlerTests.cs
using Application.Features.Auth.Commands.RefreshToken;
using Application.Features.Auth.DTOs;
using Application.Interfaces;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Common;
using Domain.Entities.Retailer;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.DTOs;
using System.Security.Claims;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Auth;

public sealed class RefreshTokenCommandHandlerTests : TestBase
{
    private const string RawRefreshToken = "raw-refresh-token-value";
    private static readonly string HashedRefreshToken =
        BCrypt.Net.BCrypt.HashPassword(RawRefreshToken, workFactor: 4);

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<RefreshTokenCommandHandler>> _loggerMock;
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _retailerRepoMock = MockRepository.Create<IRepository<RetailerAccount>>();
        _tokenServiceMock = MockRepository.Create<ITokenService>();
        _loggerMock = new Mock<ILogger<RefreshTokenCommandHandler>>();

        _unitOfWorkMock.Setup(u => u.Repository<RetailerAccount>()).Returns(_retailerRepoMock.Object);

        _handler = new RefreshTokenCommandHandler(
            _unitOfWorkMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ClaimsPrincipal BuildPrincipalWithSub(Guid retailerId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, retailerId.ToString()),
        });
        return new ClaimsPrincipal(identity);
    }

    private RetailerAccount BuildActiveAccountWithValidToken(Guid retailerId)
    {
        var account = RetailerAccount.Create(
            "Test Retailer", "retailer@example.com",
            BCrypt.Net.BCrypt.HashPassword("P@ssw0rd1!", workFactor: 4),
            "TestBrand");

        typeof(BaseEntity).GetProperty("Id")!.SetValue(account, retailerId);
        typeof(RetailerAccount).GetProperty("AccountStatus")!.SetValue(account, RetailerAccount.Status.Active);

        // Set valid refresh token (hash + future expiry)
        account.UpdateRefreshToken(HashedRefreshToken, DateTime.UtcNow.AddDays(7), rememberMe: false);
        return account;
    }

    private RetailerAccount BuildActiveAccountWithExpiredToken(Guid retailerId)
    {
        var account = BuildActiveAccountWithValidToken(retailerId);
        // Overwrite expiry to past
        account.UpdateRefreshToken(HashedRefreshToken, DateTime.UtcNow.AddDays(-1), rememberMe: false);
        return account;
    }

    [Fact]
    public async Task Handle_ExpiredRefreshToken_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        var principal = BuildPrincipalWithSub(retailerId);
        var account = BuildActiveAccountWithExpiredToken(retailerId);

        _tokenServiceMock
            .Setup(t => t.GetClaimsFromExpiredToken("expired-access-token"))
            .Returns(principal);

        _retailerRepoMock
            .Setup(r => r.GetByIdAsync(retailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var command = new RefreshTokenCommand("expired-access-token", RawRefreshToken);

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsNewAccessTokenAndRotatedRefreshToken()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        var principal = BuildPrincipalWithSub(retailerId);
        var account = BuildActiveAccountWithValidToken(retailerId);

        _tokenServiceMock
            .Setup(t => t.GetClaimsFromExpiredToken("old-access-token"))
            .Returns(principal);

        _retailerRepoMock
            .Setup(r => r.GetByIdAsync(retailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _tokenServiceMock.Setup(t => t.GenerateAccessToken(account)).Returns("new-access-token");
        _tokenServiceMock.Setup(t => t.GenerateRefreshToken()).Returns("new-raw-refresh-token");

        _retailerRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Verifiable();

        var command = new RefreshTokenCommand("old-access-token", RawRefreshToken);

        // Act
        Result<AuthTokenResponse> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("new-access-token");
        result.Data.RefreshToken.Should().Be("new-raw-refresh-token");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}