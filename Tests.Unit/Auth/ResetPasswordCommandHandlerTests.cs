// tests/Tests.Unit/Auth/ResetPasswordCommandHandlerTests.cs
using Application.Features.Auth.Commands.ResetPassword;
using Application.Interfaces;
using Domain.Entities.Retailer;
using Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.DTOs;
using Tests.Unit.Auth.Helpers;
using Xunit;

namespace Tests.Unit.Auth;

public sealed class ResetPasswordCommandHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly ILogger<ResetPasswordCommandHandler> _logger =
        Substitute.For<ILogger<ResetPasswordCommandHandler>>();
    private readonly IRepository<RetailerAccount> _accountRepo =
        Substitute.For<IRepository<RetailerAccount>>();

    private readonly ResetPasswordCommandHandler _sut;

    public ResetPasswordCommandHandlerTests()
    {
        _unitOfWork.SetupRepositories(_accountRepo);
        _sut = new ResetPasswordCommandHandler(_unitOfWork, _cacheService, _logger);
    }

    private const string ValidEmail = "reset@example.com";
    private const string ValidRawOtp = "482910";
    private const string ValidNewPassword = "NewPassword1!";
    private const string OtpCacheKey = "pwd_reset:reset@example.com";

    // Real BCrypt hash of ValidRawOtp at work factor 4 — simulates what is stored in Redis
    private static readonly string ValidHashedOtp =
        BCrypt.Net.BCrypt.HashPassword(ValidRawOtp, workFactor: 4);

    // =========================================================================
    // HAPPY PATH
    // =========================================================================

    [Fact]
    public async Task Handle_ValidOtp_ReturnsSuccess()
    {
        // Arrange
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail);
        _accountRepo.ReturnsForAnyPredicate(account);
        _cacheService.GetAsync<string>(
                Arg.Is<string>(k => k.Contains(ValidEmail.ToLowerInvariant())),
                Arg.Any<CancellationToken>())
            .Returns(ValidHashedOtp);

        // FIX: No .Returns() on UpdateAsync — it returns Task, NSubstitute default is correct.

        var command = new ResetPasswordCommand(ValidEmail, ValidRawOtp, ValidNewPassword);

        // Act
        Result<bool> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        result.Message.Should().Contain("reset");
    }

    [Fact]
    public async Task Handle_ValidOtp_UpdatesPasswordHash()
    {
        // Arrange
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail, "OldPassword1!");
        string originalHash = account.PasswordHash;

        _accountRepo.ReturnsForAnyPredicate(account);
        _cacheService.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValidHashedOtp);

        var command = new ResetPasswordCommand(ValidEmail, ValidRawOtp, ValidNewPassword);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert — hash changed AND verifies against the new password
        account.PasswordHash.Should().NotBe(originalHash);
        BCrypt.Net.BCrypt.Verify(ValidNewPassword, account.PasswordHash)
            .Should().BeTrue("the new hash must validate against the new password");
    }

    [Fact]
    public async Task Handle_ValidOtp_RevokesAllRefreshTokens()
    {
        // Arrange — account has an active session
        (RetailerAccount account, _) = RetailerAccountFactory.CreateWithRefreshToken(ValidEmail);
        account.RefreshTokenHash.Should().NotBeNull("pre-condition: account must have a session");

        _accountRepo.ReturnsForAnyPredicate(account);
        _cacheService.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValidHashedOtp);

        var command = new ResetPasswordCommand(ValidEmail, ValidRawOtp, ValidNewPassword);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert — all sessions invalidated (forces re-login on every device)
        account.RefreshTokenHash.Should().BeNull();
        account.RefreshTokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidOtp_DeletesOtpFromCache_EnforcingOneTimeUse()
    {
        // Arrange
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail);
        _accountRepo.ReturnsForAnyPredicate(account);
        _cacheService.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValidHashedOtp);

        var command = new ResetPasswordCommand(ValidEmail, ValidRawOtp, ValidNewPassword);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert — OTP deleted from Redis after successful use (one-time use)
        await _cacheService.Received(1).RemoveAsync(
            Arg.Is<string>(k => k == OtpCacheKey),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // FAILURE — Expired OTP (cache miss → null)
    // =========================================================================

    [Fact]
    public async Task Handle_ExpiredOtp_ThrowsBusinessRuleException_WithCode_INVALID_OTP()
    {
        // Arrange — GetAsync returns null (OTP expired or never set in Redis)
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail);
        _accountRepo.ReturnsForAnyPredicate(account);
        _cacheService.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var command = new ResetPasswordCommand(ValidEmail, ValidRawOtp, ValidNewPassword);

        // Act
        Func<Task> act = () => _sut.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INVALID_OTP");
        ex.Which.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task Handle_ExpiredOtp_DoesNotPersistAnyChanges()
    {
        // Arrange
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail, "OldPassword1!");
        string originalHash = account.PasswordHash;

        _accountRepo.ReturnsForAnyPredicate(account);
        _cacheService.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var command = new ResetPasswordCommand(ValidEmail, ValidRawOtp, ValidNewPassword);

        // Act
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.Handle(command, CancellationToken.None));

        // Assert — password unchanged, no DB save
        account.PasswordHash.Should().Be(originalHash);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // FAILURE — Wrong OTP (BCrypt mismatch)
    // =========================================================================

    [Fact]
    public async Task Handle_WrongOtp_ThrowsBusinessRuleException_WithCode_INVALID_OTP()
    {
        // Arrange — Redis has the hash of "482910"; command sends "000000"
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail);
        _accountRepo.ReturnsForAnyPredicate(account);
        _cacheService.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValidHashedOtp);

        var command = new ResetPasswordCommand(ValidEmail, "000000", ValidNewPassword);

        // Act
        Func<Task> act = () => _sut.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INVALID_OTP");
        ex.Which.Message.Should().Contain("incorrect");
    }

    [Fact]
    public async Task Handle_WrongOtp_DoesNotDeleteOtpFromCache()
    {
        // Security test: deleting the OTP on a wrong guess would be a DoS vector —
        // any attacker could clear the legitimate user's OTP by guessing any wrong code.
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail);
        _accountRepo.ReturnsForAnyPredicate(account);
        _cacheService.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValidHashedOtp);

        var command = new ResetPasswordCommand(ValidEmail, "000000", ValidNewPassword);

        // Act
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.Handle(command, CancellationToken.None));

        // Assert — OTP must remain in Redis
        await _cacheService.DidNotReceive().RemoveAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // FAILURE — Email Not Found (returns INVALID_OTP to prevent enumeration)
    // =========================================================================

    [Fact]
    public async Task Handle_EmailNotFound_ThrowsBusinessRuleException_WithCode_INVALID_OTP()
    {
        // The handler returns INVALID_OTP instead of NotFoundException so that an attacker
        // cannot use the reset endpoint to discover which emails are registered.
        _accountRepo.ReturnsForAnyPredicate<RetailerAccount>(null);

        var command = new ResetPasswordCommand("ghost@example.com", ValidRawOtp, ValidNewPassword);

        // Act
        Func<Task> act = () => _sut.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INVALID_OTP",
            because: "email not found must return the same error as a wrong OTP " +
                     "to prevent email enumeration via the password reset endpoint");
    }
}