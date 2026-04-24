// tests/Tests.Unit/Application/Features/Auth/LoginCommandHandlerTests.cs
using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.DTOs;
using Application.Interfaces;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.DTOs;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Auth;

public sealed class LoginCommandHandlerTests : TestBase
{
    // ── Pre-computed BCrypt hash (work-factor 4) for fast tests ──────────────
    private const string ValidPlainPassword = "P@ssw0rd1!";
    private static readonly string ValidPasswordHash =
        BCrypt.Net.BCrypt.HashPassword(ValidPlainPassword, workFactor: 4);

    // ── Mocks ─────────────────────────────────────────────────────────────────
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<LoginCommandHandler>> _loggerMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _retailerRepoMock = MockRepository.Create<IRepository<RetailerAccount>>();
        _tokenServiceMock = MockRepository.Create<ITokenService>();
        _loggerMock = new Mock<ILogger<LoginCommandHandler>>();

        _unitOfWorkMock
            .Setup(u => u.Repository<RetailerAccount>())
            .Returns(_retailerRepoMock.Object);

        _handler = new LoginCommandHandler(
            _unitOfWorkMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RetailerAccount BuildActiveAccount(string email, string passwordHash)
    {
        var account = RetailerAccount.Create("Test Retailer", email, passwordHash, "TestBrand");
        // Activate via reflection (Status setter is private)
        typeof(RetailerAccount)
            .GetProperty("AccountStatus")!
            .SetValue(account, RetailerAccount.Status.Active);
        return account;
    }

    private void SetupHappyPathPersistence()
    {
        _retailerRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Verifiable();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsAuthResponseWithNonNullTokens()
    {
        // Arrange
        const string email = "retailer@example.com";
        var account = BuildActiveAccount(email, ValidPasswordHash);

        _retailerRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _tokenServiceMock.Setup(t => t.GenerateAccessToken(account)).Returns("access-token-123");
        _tokenServiceMock.Setup(t => t.GenerateRefreshToken()).Returns("raw-refresh-token-456");
        SetupHappyPathPersistence();

        var command = new LoginCommand(email, ValidPlainPassword, RememberMe: false);

        // Act
        Result<AuthTokenResponse> result =
            await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("access-token-123");
        result.Data.RefreshToken.Should().Be("raw-refresh-token-456");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailNotFound_ThrowsUnauthorizedAccessException()
    {
        // Arrange — repository returns null (email absent in DB)
        _retailerRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount?)null);

        var command = new LoginCommand("unknown@example.com", "any-password", false);

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task Handle_WrongPassword_IncrementsFailedCountAndThrowsUnauthorizedAccessException()
    {
        // Arrange — account has hash of a DIFFERENT password
        const string email = "retailer@example.com";
        string wrongHash = BCrypt.Net.BCrypt.HashPassword("DifferentPass1!", workFactor: 4);
        var account = BuildActiveAccount(email, wrongHash);

        _retailerRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Handler will update and save after incrementing failed count
        _retailerRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new LoginCommand(email, ValidPlainPassword, false);

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SoftDeletedRetailer_ThrowsUnauthorizedAccessException()
    {
        // Arrange — global query filter excludes soft-deleted accounts, so repo returns null
        _retailerRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount?)null);

        var command = new LoginCommand("deleted@example.com", ValidPlainPassword, false);

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task Handle_RetailerWithInactiveSubscription_LoginSucceedsWithoutSubscriptionCheck()
    {
        // Arrange — subscription status is never evaluated in LoginCommandHandler
        const string email = "retailer@example.com";
        var account = BuildActiveAccount(email, ValidPasswordHash);

        _retailerRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _tokenServiceMock.Setup(t => t.GenerateAccessToken(account)).Returns("at");
        _tokenServiceMock.Setup(t => t.GenerateRefreshToken()).Returns("rt");
        SetupHappyPathPersistence();

        var command = new LoginCommand(email, ValidPlainPassword, false);

        // Act
        Result<AuthTokenResponse> result =
            await _handler.Handle(command, CancellationToken.None);

        // Assert — subscription check is per-request, NOT at login time
        result.IsSuccess.Should().BeTrue(
            "subscription status must not block login — it is checked per-request elsewhere");
    }
}