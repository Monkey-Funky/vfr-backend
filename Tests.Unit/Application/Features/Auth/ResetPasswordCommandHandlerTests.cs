// tests/Tests.Unit/Application/Features/Auth/ResetPasswordCommandHandlerTests.cs
using Application.Features.Auth.Commands.ResetPassword;
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

public sealed class ResetPasswordCommandHandlerTests : TestBase
{
    private const string ValidOtpCode = "123456";
    private static readonly string HashedOtp =
        BCrypt.Net.BCrypt.HashPassword(ValidOtpCode, workFactor: 4);

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<ILogger<ResetPasswordCommandHandler>> _loggerMock;
    private readonly ResetPasswordCommandHandler _handler;

    public ResetPasswordCommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _retailerRepoMock = MockRepository.Create<IRepository<RetailerAccount>>();
        _cacheMock = MockRepository.Create<ICacheService>();
        _loggerMock = new Mock<ILogger<ResetPasswordCommandHandler>>();

        _unitOfWorkMock.Setup(u => u.Repository<RetailerAccount>()).Returns(_retailerRepoMock.Object);

        _handler = new ResetPasswordCommandHandler(
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RetailerAccount BuildActiveAccount(string email)
    {
        var account = RetailerAccount.Create(
            "Reset User", email,
            BCrypt.Net.BCrypt.HashPassword("OldP@ss1!", workFactor: 4),
            "ResetBrand");
        typeof(RetailerAccount)
            .GetProperty("AccountStatus")!
            .SetValue(account, RetailerAccount.Status.Active);
        return account;
    }

    [Fact]
    public async Task Handle_OtpNotInCache_ThrowsBusinessRuleException()
    {
        // Arrange — account found but no OTP in Redis (expired)
        var account = BuildActiveAccount("user@example.com");

        _retailerRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _cacheMock
            .Setup(c => c.GetAsync<string>(
                It.Is<string>(k => k.StartsWith("pwd_reset:")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null); // OTP not in cache (expired)

        var command = new ResetPasswordCommand("user@example.com", ValidOtpCode, "NewP@ss1!");

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*INVALID_OTP*");
    }

    [Fact]
    public async Task Handle_ValidOtp_UpdatesPasswordHashAndRevokesAllRefreshTokens()
    {
        // Arrange
        var account = BuildActiveAccount("user@example.com");

        // Give the account an active refresh token first
        account.UpdateRefreshToken(
            BCrypt.Net.BCrypt.HashPassword("some-refresh", workFactor: 4),
            DateTime.UtcNow.AddDays(7),
            rememberMe: false);

        _retailerRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _cacheMock
            .Setup(c => c.GetAsync<string>(
                It.Is<string>(k => k.StartsWith("pwd_reset:")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(HashedOtp);

        _cacheMock
            .Setup(c => c.RemoveAsync(
                It.Is<string>(k => k.StartsWith("pwd_reset:")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _retailerRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Verifiable();

        var command = new ResetPasswordCommand("user@example.com", ValidOtpCode, "BrandNewP@ss1!");

        // Act
        Result<bool> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Password should have changed (new BCrypt hash is different from old)
        account.PasswordHash.Should().NotContain("OldP@ss1",
            "password hash must have been replaced by a new hash");

        // Refresh token must be revoked
        account.RefreshTokenHash.Should().BeNull("all refresh tokens must be revoked on password reset");
        account.RefreshTokenExpiresAt.Should().BeNull();

        _cacheMock.Verify(
            c => c.RemoveAsync(
                It.Is<string>(k => k.StartsWith("pwd_reset:")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}