// tests/Tests.Unit/Application/Features/Auth/RegisterStep2CommandHandlerTests.cs
using Application.Features.Auth.Commands.RegisterStep2;
using Application.Features.Auth.DTOs;
using Application.Interfaces;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Common;
using Domain.Entities.Retailer;
using Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.DTOs;
using System.Security.Claims;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Auth;

public sealed class RegisterStep2CommandHandlerTests : TestBase
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock;
    private readonly Mock<IRepository<NotificationPreference>> _prefRepoMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILogger<RegisterStep2CommandHandler>> _loggerMock;
    private readonly RegisterStep2CommandHandler _handler;

    public RegisterStep2CommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _retailerRepoMock = MockRepository.Create<IRepository<RetailerAccount>>();
        _prefRepoMock = MockRepository.Create<IRepository<NotificationPreference>>();
        _tokenServiceMock = MockRepository.Create<ITokenService>();
        _fileStorageMock = MockRepository.Create<IFileStorageService>();
        _emailServiceMock = MockRepository.Create<IEmailService>();
        _loggerMock = new Mock<ILogger<RegisterStep2CommandHandler>>();

        _unitOfWorkMock.Setup(u => u.Repository<RetailerAccount>()).Returns(_retailerRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Repository<NotificationPreference>()).Returns(_prefRepoMock.Object);

        _handler = new RegisterStep2CommandHandler(
            _unitOfWorkMock.Object,
            _tokenServiceMock.Object,
            _fileStorageMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ClaimsPrincipal BuildValidStepPrincipal(Guid accountId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("token_type",      "step"),
            new Claim("temp_account_id", accountId.ToString()),
            new Claim("step",            "1"),
        });
        return new ClaimsPrincipal(identity);
    }

    private static RetailerAccount BuildPendingAccount(Guid id)
    {
        var account = RetailerAccount.Create("John Retailer", "john@example.com",
            BCrypt.Net.BCrypt.HashPassword("P@ssw0rd1!", workFactor: 4), "JohnBrand");
        typeof(BaseEntity).GetProperty("Id")!.SetValue(account, id);
        return account;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_InvalidVerificationToken_ThrowsUnauthorizedAccessException()
    {
        // Arrange — ValidateTempStepToken returns null for invalid/expired token
        _tokenServiceMock
            .Setup(t => t.ValidateTempStepToken(It.IsAny<string>()))
            .Returns((ClaimsPrincipal?)null);

        var command = new RegisterStep2Command(
            TempStepToken: "invalid-token",
            BusinessType: "Fashion",
            Has3DModels: false,
            BrandLogoStream: null,
            BrandLogoFileName: null,
            BrandLogoContentType: null,
            BrandLogoSizeBytes: 0);

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*invalid or has expired*");
    }

    [Fact]
    public async Task Handle_ValidToken_ActivatesRetailerAndCreatesNotificationPreference()
    {
        // Arrange
        Guid accountId = Guid.NewGuid();
        var principal = BuildValidStepPrincipal(accountId);
        var account = BuildPendingAccount(accountId);

        _tokenServiceMock.Setup(t => t.ValidateTempStepToken("valid-step-token")).Returns(principal);

        _retailerRepoMock
            .Setup(r => r.GetByIdAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // ExecuteInTransactionAsync — invoke the lambda synchronously
        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Func<CancellationToken, Task>, CancellationToken>(
                (action, ct) => action(ct).GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _retailerRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        NotificationPreference? savedPref = null;

        // FIX: IRepository<T>.AddAsync returns Task<T>, not Task.
        //      Merge the Callback into a Returns<T, CancellationToken> delegate so the
        //      return type matches Task<NotificationPreference> exactly.
        _prefRepoMock
            .Setup(r => r.AddAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()))
            .Returns<NotificationPreference, CancellationToken>((pref, _) =>
            {
                savedPref = pref;
                return Task.FromResult(pref);
            });

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _tokenServiceMock.Setup(t => t.GenerateAccessToken(It.IsAny<RetailerAccount>())).Returns("new-at");
        _tokenServiceMock.Setup(t => t.GenerateRefreshToken()).Returns("new-rt");

        _retailerRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _emailServiceMock
            .Setup(e => e.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new RegisterStep2Command(
            TempStepToken: "valid-step-token",
            BusinessType: "Fashion",
            Has3DModels: true,
            BrandLogoStream: null,
            BrandLogoFileName: null,
            BrandLogoContentType: null,
            BrandLogoSizeBytes: 0);

        // Act
        Result<AuthTokenResponse> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("new-at");
        result.Data.RefreshToken.Should().Be("new-rt");

        account.AccountStatus.Should().Be(RetailerAccount.Status.Active,
            "CompleteRegistration must have activated the account");

        savedPref.Should().NotBeNull("a NotificationPreference must be seeded atomically");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}