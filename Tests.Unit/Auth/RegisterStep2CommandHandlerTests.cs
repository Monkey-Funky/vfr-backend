// tests/Tests.Unit/Auth/RegisterStep2CommandHandlerTests.cs
using Application.Features.Auth.Commands.RegisterStep2;
using Application.Features.Auth.DTOs;
using Application.Interfaces;
using Domain.Entities.Retailer;
using Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.DTOs;
using System.Security.Claims;
using Tests.Unit.Auth.Helpers;
using Xunit;

namespace Tests.Unit.Auth;

public sealed class RegisterStep2CommandHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IFileStorageService _fileStorageService = Substitute.For<IFileStorageService>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly ILogger<RegisterStep2CommandHandler> _logger =
        Substitute.For<ILogger<RegisterStep2CommandHandler>>();
    private readonly IRepository<RetailerAccount> _accountRepo =
        Substitute.For<IRepository<RetailerAccount>>();
    private readonly IRepository<NotificationPreference> _prefRepo =
        Substitute.For<IRepository<NotificationPreference>>();

    private readonly RegisterStep2CommandHandler _sut;

    public RegisterStep2CommandHandlerTests()
    {
        _unitOfWork.SetupRepositories(_accountRepo, _prefRepo);
        _sut = new RegisterStep2CommandHandler(
            _unitOfWork, _tokenService, _fileStorageService, _emailService, _logger);
    }

    private static RegisterStep2Command ValidCommand() => new(
        TempStepToken: "valid-step-token",
        BusinessType: "Fashion",
        Has3DModels: false,
        BrandLogoStream: null,
        BrandLogoFileName: null,
        BrandLogoContentType: null,
        BrandLogoSizeBytes: 0);

    private static ClaimsPrincipal BuildValidStepPrincipal(Guid accountId) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim("token_type", "step"),
            new Claim("step", "1"),
            new Claim("temp_account_id", accountId.ToString()),
        }));

    /// <summary>Sets up all substitutes for the successful Step 2 path.</summary>
    private void ArrangeHappyPath(RetailerAccount pendingAccount)
    {
        _tokenService.ValidateTempStepToken(Arg.Any<string>())
            .Returns(BuildValidStepPrincipal(pendingAccount.Id));

        // FIX: Use Task.FromResult<RetailerAccount?> to match GetByIdAsync's Task<T?> return type.
        // Passing a non-nullable T directly caused the nullability mismatch warning.
        _accountRepo.GetByIdAsync(pendingAccount.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RetailerAccount?>(pendingAccount));

        _tokenService.GenerateAccessToken(Arg.Any<RetailerAccount>()).Returns("fake-access-token");
        _tokenService.GenerateRefreshToken().Returns("fake-refresh-token");

        // FIX: No .Returns() on UpdateAsync (Task) or AddAsync — handlers discard both.
    }

    // =========================================================================
    // HAPPY PATH
    // =========================================================================

    [Fact]
    public async Task Handle_ValidStepToken_ReturnsAuthTokenResponse()
    {
        // Arrange
        RetailerAccount pending = RetailerAccountFactory.CreatePending();
        ArrangeHappyPath(pending);

        // Act
        Result<AuthTokenResponse> result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("fake-access-token");
        result.Data.RefreshToken.Should().Be("fake-refresh-token");
    }

    [Fact]
    public async Task Handle_ValidStepToken_SetsAccountStatusToActiveAndVerifiesEmail()
    {
        // Arrange
        RetailerAccount pending = RetailerAccountFactory.CreatePending();
        ArrangeHappyPath(pending);

        // Act
        await _sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert — FIX F-05: CompleteRegistration sets BOTH Status AND IsEmailVerified
        pending.AccountStatus.Should().Be(RetailerAccount.Status.Active);
        pending.IsEmailVerified.Should().BeTrue(
            because: "CompleteRegistration sets IsEmailVerified = true (FIX F-05)");
    }

    [Fact]
    public async Task Handle_ValidStepToken_SeedsNotificationPreferencesWithAllDefaults()
    {
        // Arrange
        RetailerAccount pending = RetailerAccountFactory.CreatePending();
        ArrangeHappyPath(pending);

        // Act
        await _sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert — all five flags must be true (NotificationPreference.CreateDefault defaults)
        await _prefRepo.Received(1).AddAsync(
            Arg.Is<NotificationPreference>(p =>
                p.RetailerId == pending.Id
                && p.LowStockAlerts
                && p.OrderStatusAlerts
                && p.SubscriptionAlerts
                && p.EmailNotifications
                && p.InAppNotifications),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // FAILURE — Invalid / Expired Step Token
    // =========================================================================

    [Fact]
    public async Task Handle_InvalidStepToken_ThrowsUnauthorizedException()
    {
        // Arrange — null means the token is expired, tampered, or structurally invalid
        _tokenService.ValidateTempStepToken(Arg.Any<string>())
            .Returns((ClaimsPrincipal?)null);

        // Act
        Func<Task> act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*invalid or has expired*");
    }

    [Fact]
    public async Task Handle_WrongTokenType_ThrowsUnauthorizedException_EvenWithValidSignature()
    {
        // Arrange — HS256 signature is valid (non-null principal) but token_type is wrong (FIX F-02)
        var wrongTypePrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("token_type", "access"),  // wrong — must be "step"
            new Claim("step", "1"),
            new Claim("temp_account_id", Guid.NewGuid().ToString()),
        }));

        _tokenService.ValidateTempStepToken(Arg.Any<string>())
            .Returns(wrongTypePrincipal);

        // Act
        Func<Task> act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert — valid signature alone is not enough; token_type must be "step" (FIX F-02)
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*not a valid registration step token*");
    }

    [Fact]
    public async Task Handle_AlreadyActiveAccount_ThrowsBusinessRuleException_ReplayAttackGuard()
    {
        // Arrange — account is already Active (replay: reusing the Step 1 token)
        RetailerAccount active = RetailerAccountFactory.CreateActive();

        _tokenService.ValidateTempStepToken(Arg.Any<string>())
            .Returns(BuildValidStepPrincipal(active.Id));

        // FIX: Task.FromResult<RetailerAccount?> for GetByIdAsync
        _accountRepo.GetByIdAsync(active.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RetailerAccount?>(active));

        // Act
        Func<Task> act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("REGISTRATION_ALREADY_COMPLETED");
    }
}