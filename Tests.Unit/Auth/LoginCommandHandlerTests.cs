// tests/Tests.Unit/Auth/LoginCommandHandlerTests.cs
using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.DTOs;
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

public sealed class LoginCommandHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly ILogger<LoginCommandHandler> _logger =
        Substitute.For<ILogger<LoginCommandHandler>>();
    private readonly IRepository<RetailerAccount> _accountRepo =
        Substitute.For<IRepository<RetailerAccount>>();

    private readonly LoginCommandHandler _sut;

    public LoginCommandHandlerTests()
    {
        _unitOfWork.SetupRepositories(_accountRepo);
        _sut = new LoginCommandHandler(_unitOfWork, _tokenService, _logger);
    }

    private const string ValidEmail = "retailer@example.com";
    private const string ValidPassword = "Password1!";
    private const string FakeAccessToken = "eyJfake.access.token";
    private const string FakeRefreshToken = "fake-raw-refresh-token-abc123";

    // =========================================================================
    // HAPPY PATH
    // =========================================================================

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsAuthTokenResponseWithTokens()
    {
        // Arrange
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail, ValidPassword);
        _accountRepo.ReturnsForAnyPredicate(account);

        // FIX: No .Returns() on UpdateAsync — it returns Task (not Task<T>).
        // NSubstitute's default Task.CompletedTask is correct.
        _tokenService.GenerateAccessToken(account).Returns(FakeAccessToken);
        _tokenService.GenerateRefreshToken().Returns(FakeRefreshToken);

        var command = new LoginCommand(ValidEmail, ValidPassword, RememberMe: false);

        // Act
        Result<AuthTokenResponse> result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be(FakeAccessToken);
        result.Data.RefreshToken.Should().Be(FakeRefreshToken);
        result.Data.ExpiresIn.Should().Be(900); // 15 minutes
        result.Data.RetailerProfile.Email.Should().Be(account.Email);
        result.Data.RetailerProfile.AccountStatus.Should().Be(RetailerAccount.Status.Active);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ResetsFailedLoginCount()
    {
        // Arrange — account has 2 previous failures (not yet locked)
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail, ValidPassword);
        account.IncrementFailedLoginCount();
        account.IncrementFailedLoginCount();

        _accountRepo.ReturnsForAnyPredicate(account);
        _tokenService.GenerateAccessToken(Arg.Any<RetailerAccount>()).Returns(FakeAccessToken);
        _tokenService.GenerateRefreshToken().Returns(FakeRefreshToken);

        var command = new LoginCommand(ValidEmail, ValidPassword, RememberMe: false);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert — success resets the counter
        account.AccessFailedCount.Should().Be(0);
        account.LockoutEndAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RememberMeTrue_Sets30DayRefreshTokenExpiryOnAccount()
    {
        // Arrange
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail, ValidPassword);
        _accountRepo.ReturnsForAnyPredicate(account);
        _tokenService.GenerateAccessToken(Arg.Any<RetailerAccount>()).Returns(FakeAccessToken);
        _tokenService.GenerateRefreshToken().Returns(FakeRefreshToken);

        var command = new LoginCommand(ValidEmail, ValidPassword, RememberMe: true);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert — expiry must be ~30 days from now (allow ±5 s for test timing)
        account.RefreshTokenExpiresAt.Should().BeCloseTo(
            DateTime.UtcNow.AddDays(30), precision: TimeSpan.FromSeconds(5));
        account.IsRememberMeSession.Should().BeTrue();
    }

    // =========================================================================
    // FAILURE — Wrong Password
    // =========================================================================

    [Fact]
    public async Task Handle_WrongPassword_ThrowsUnauthorizedException()
    {
        // Arrange — account hashed with "CorrectPassword1!", command sends a different one
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail, "CorrectPassword1!");
        _accountRepo.ReturnsForAnyPredicate(account);

        var command = new LoginCommand(ValidEmail, "WrongPassword999!", RememberMe: false);

        // Act
        Func<Task> act = () => _sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task Handle_WrongPassword_IncrementsAccessFailedCount()
    {
        // Arrange
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail, "CorrectPassword1!");
        _accountRepo.ReturnsForAnyPredicate(account);
        int initialCount = account.AccessFailedCount; // 0

        var command = new LoginCommand(ValidEmail, "WrongPassword999!", RememberMe: false);

        // Act
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.Handle(command, CancellationToken.None));

        // Assert — incremented by exactly 1
        account.AccessFailedCount.Should().Be(initialCount + 1);
    }

    [Fact]
    public async Task Handle_WrongPassword_PersistsFailedCountToDatabase()
    {
        // Arrange
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail, "CorrectPassword1!");
        _accountRepo.ReturnsForAnyPredicate(account);

        var command = new LoginCommand(ValidEmail, "WrongPassword999!", RememberMe: false);

        // Act
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.Handle(command, CancellationToken.None));

        // Assert — UpdateAsync AND SaveChangesAsync must be called on the failure path
        await _accountRepo.Received(1).UpdateAsync(
            Arg.Is<RetailerAccount>(a => a.AccessFailedCount > 0),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // FAILURE — Account Locked
    // =========================================================================

    [Fact]
    public async Task Handle_AccountLocked_ThrowsBusinessRuleException_WithCode_ACCOUNT_LOCKED()
    {
        // Arrange
        RetailerAccount locked = RetailerAccountFactory.CreateLockedOut(ValidEmail, ValidPassword);
        locked.IsLockedOut().Should().BeTrue("pre-condition");
        _accountRepo.ReturnsForAnyPredicate(locked);

        var command = new LoginCommand(ValidEmail, ValidPassword, RememberMe: false);

        // Act
        Func<Task> act = () => _sut.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("ACCOUNT_LOCKED");
        ex.Which.Message.Should().Contain("locked");
    }

    [Fact]
    public async Task Handle_AccountLocked_DoesNotCallTokenService()
    {
        // Arrange
        RetailerAccount locked = RetailerAccountFactory.CreateLockedOut(ValidEmail, ValidPassword);
        _accountRepo.ReturnsForAnyPredicate(locked);

        // Act
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.Handle(new LoginCommand(ValidEmail, ValidPassword, false), CancellationToken.None));

        // Assert — no tokens issued for a locked account
        _tokenService.DidNotReceive().GenerateAccessToken(Arg.Any<RetailerAccount>());
        _tokenService.DidNotReceive().GenerateRefreshToken();
    }

    // =========================================================================
    // FAILURE — Email Not Verified (PendingEmailVerification)
    // =========================================================================

    [Fact]
    public async Task Handle_EmailNotVerified_ThrowsBusinessRuleException_WithCode_EMAIL_NOT_VERIFIED()
    {
        // Arrange
        RetailerAccount pending = RetailerAccountFactory.CreatePending(ValidEmail, ValidPassword);
        pending.AccountStatus.Should().Be(RetailerAccount.Status.PendingEmailVerification, "pre-condition");
        _accountRepo.ReturnsForAnyPredicate(pending);

        var command = new LoginCommand(ValidEmail, ValidPassword, RememberMe: false);

        // Act
        Func<Task> act = () => _sut.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("EMAIL_NOT_VERIFIED");
    }

    [Fact]
    public async Task Handle_AccountLockedAndPending_LockoutTakesPriorityOverStatus()
    {
        // FIX F-04 verification: lockout guard must run BEFORE status guard.
        // A pending+locked account must return ACCOUNT_LOCKED, not EMAIL_NOT_VERIFIED.
        RetailerAccount pendingAndLocked = RetailerAccountFactory.CreatePending(ValidEmail, ValidPassword);
        for (int i = 0; i < 10; i++)
            pendingAndLocked.IncrementFailedLoginCount();

        _accountRepo.ReturnsForAnyPredicate(pendingAndLocked);

        var command = new LoginCommand(ValidEmail, ValidPassword, RememberMe: false);

        // Act
        Func<Task> act = () => _sut.Handle(command, CancellationToken.None);

        // Assert — must be ACCOUNT_LOCKED (not EMAIL_NOT_VERIFIED)
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("ACCOUNT_LOCKED",
            because: "lockout check runs before status check per FIX F-04");
    }

    // =========================================================================
    // FAILURE — Email Not Found
    // =========================================================================

    [Fact]
    public async Task Handle_EmailNotFound_ThrowsUnauthorizedException()
    {
        // Arrange — null = no account found
        _accountRepo.ReturnsForAnyPredicate<RetailerAccount>(null);

        var command = new LoginCommand("nobody@example.com", ValidPassword, RememberMe: false);

        // Act
        Func<Task> act = () => _sut.Handle(command, CancellationToken.None);

        // Assert — generic message, prevents email enumeration
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task Handle_EmailNotFound_SameErrorMessageAsWrongPassword()
    {
        // Security test — the error message must be identical for both paths
        // so an attacker cannot distinguish "email exists" from "email doesn't exist".

        // Path A: email not found
        _accountRepo.ReturnsForAnyPredicate<RetailerAccount>(null);
        UnauthorizedException? notFoundEx = null;
        try
        {
            await _sut.Handle(new LoginCommand("ghost@example.com", "any", false),
                CancellationToken.None);
        }
        catch (UnauthorizedException ex) { notFoundEx = ex; }

        // Path B: wrong password
        RetailerAccount account = RetailerAccountFactory.CreateActive(ValidEmail, "CorrectPassword1!");
        _accountRepo.ReturnsForAnyPredicate(account);
        UnauthorizedException? wrongPassEx = null;
        try
        {
            await _sut.Handle(new LoginCommand(ValidEmail, "WrongPassword999!", false),
                CancellationToken.None);
        }
        catch (UnauthorizedException ex) { wrongPassEx = ex; }

        // Assert — identical messages
        notFoundEx.Should().NotBeNull();
        wrongPassEx.Should().NotBeNull();
        notFoundEx!.Message.Should().Be(wrongPassEx!.Message,
            because: "differing messages enable email enumeration");
    }
}