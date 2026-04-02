// tests/Tests.Unit/Auth/ForgotPasswordCommandHandlerTests.cs
using Application.Features.Auth.Commands.ForgotPassword;
using Application.Interfaces;
using Domain.Entities.Retailer;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.DTOs;
using Tests.Unit.Auth.Helpers;
using Xunit;

namespace Tests.Unit.Auth;

public sealed class ForgotPasswordCommandHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly ILogger<ForgotPasswordCommandHandler> _logger =
        Substitute.For<ILogger<ForgotPasswordCommandHandler>>();
    private readonly IRepository<RetailerAccount> _accountRepo =
        Substitute.For<IRepository<RetailerAccount>>();

    private readonly ForgotPasswordCommandHandler _sut;

    public ForgotPasswordCommandHandlerTests()
    {
        _unitOfWork.SetupRepositories(_accountRepo);
        _sut = new ForgotPasswordCommandHandler(
            _unitOfWork, _cacheService, _emailService, _logger);
    }

    private const string KnownEmail = "active@example.com";
    private const string UnknownEmail = "ghost@example.com";

    // =========================================================================
    // HAPPY PATH — Known Active Email
    // =========================================================================

    [Fact]
    public async Task Handle_KnownActiveEmail_ReturnsSuccess()
    {
        // Arrange
        RetailerAccount active = RetailerAccountFactory.CreateActive(KnownEmail);
        _accountRepo.ReturnsForAnyPredicate(active);

        // Act
        Result<bool> result = await _sut.Handle(
            new ForgotPasswordCommand(KnownEmail), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        result.Message.Should().Contain("If an account with that email exists");
    }

    [Fact]
    public async Task Handle_KnownActiveEmail_CachesHashedOtp_NotRawDigits()
    {
        // Arrange
        RetailerAccount active = RetailerAccountFactory.CreateActive(KnownEmail);
        _accountRepo.ReturnsForAnyPredicate(active);

        string? capturedCacheValue = null;
        _cacheService.SetAsync(
            Arg.Any<string>(),
            Arg.Do<string>(v => capturedCacheValue = v),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());

        // Act
        await _sut.Handle(new ForgotPasswordCommand(KnownEmail), CancellationToken.None);

        // Assert — SetAsync called once with the correct key prefix and 15-minute TTL
        await _cacheService.Received(1).SetAsync(
            Arg.Is<string>(k => k.StartsWith("pwd_reset:")),
            Arg.Any<string>(),
            Arg.Is<TimeSpan>(t => t.TotalMinutes == 15),
            Arg.Any<CancellationToken>());

        // The cached value must be a BCrypt hash (never the raw 6-digit OTP)
        capturedCacheValue.Should().NotBeNull();
        capturedCacheValue!.Should().StartWith("$2",
            because: "the OTP must be stored as a BCrypt hash, never plain text");
        capturedCacheValue.Should().NotMatchRegex(@"^\d{6}$",
            because: "the raw OTP must never appear in the cache");
    }

    [Fact]
    public async Task Handle_KnownActiveEmail_SendsEmailContaining6DigitOtp()
    {
        // Arrange
        RetailerAccount active = RetailerAccountFactory.CreateActive(KnownEmail);
        _accountRepo.ReturnsForAnyPredicate(active);

        string? capturedBody = null;
        _emailService.SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<string>(b => capturedBody = b),
            Arg.Any<CancellationToken>());

        // Act
        await _sut.Handle(new ForgotPasswordCommand(KnownEmail), CancellationToken.None);

        // Assert — exactly one email sent to the correct address
        await _emailService.Received(1).SendEmailAsync(
            Arg.Is<string>(to => to == KnownEmail),
            Arg.Is<string>(sub => sub.Contains("Reset")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // Email body must contain the 6-digit OTP
        capturedBody.Should().NotBeNullOrEmpty();
        capturedBody!.Should().MatchRegex(@"\b\d{6}\b",
            because: "the email body must contain the 6-digit OTP");
    }

    // =========================================================================
    // ANTI-ENUMERATION — Unknown Email → identical 200 response
    // =========================================================================

    [Fact]
    public async Task Handle_UnknownEmail_ReturnsSameSuccessWithoutSendingAnything()
    {
        // Arrange — null: email not found (or not Active)
        _accountRepo.ReturnsForAnyPredicate<RetailerAccount>(null);

        // Act
        Result<bool> result = await _sut.Handle(
            new ForgotPasswordCommand(UnknownEmail), CancellationToken.None);

        // Assert — identical 200 response (anti-enumeration)
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        result.Message.Should().Contain("If an account with that email exists");

        await _emailService.DidNotReceive().SendEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _cacheService.DidNotReceive().SetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_KnownAndUnknown_ResponseMessagesAreIdentical()
    {
        // Security test — both paths must return the EXACT same message
        RetailerAccount active = RetailerAccountFactory.CreateActive(KnownEmail);

        _accountRepo.ReturnsForAnyPredicate(active);
        var knownResult = await _sut.Handle(
            new ForgotPasswordCommand(KnownEmail), CancellationToken.None);

        _accountRepo.ReturnsForAnyPredicate<RetailerAccount>(null);
        var unknownResult = await _sut.Handle(
            new ForgotPasswordCommand(UnknownEmail), CancellationToken.None);

        knownResult.Message.Should().Be(unknownResult.Message,
            because: "different messages would enable email enumeration");
    }

    [Fact]
    public async Task Handle_InactiveAccount_ReturnsSameSuccessAsUnknownEmail()
    {
        // Handler filters for Active accounts; suspended/pending accounts return null
        // from the repository and must behave identically to an unknown email.
        _accountRepo.ReturnsForAnyPredicate<RetailerAccount>(null);

        Result<bool> result = await _sut.Handle(
            new ForgotPasswordCommand("suspended@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _emailService.DidNotReceive().SendEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}