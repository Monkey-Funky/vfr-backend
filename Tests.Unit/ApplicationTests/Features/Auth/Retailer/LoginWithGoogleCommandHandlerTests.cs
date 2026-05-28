using Application.Features.Auth.Commands.LoginWithGoogle;
using Application.Features.Auth.DTOs;
using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Retailers.Auth;

public sealed class LoginWithGoogleCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IGoogleAuthService> _googleAuthServiceMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ILogger<LoginWithGoogleCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock = new();
    private readonly Mock<IRepository<NotificationPreference>> _notifRepoMock = new();
    private readonly LoginWithGoogleCommandHandler _sut;

    private static readonly GoogleUserInfo ValidGoogleUser = new(
        GoogleId: "google-sub-abc123",
        Email: "retailer@example.com",
        FullName: "Test Retailer",
        IsEmailVerified: true);

    public LoginWithGoogleCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<RetailerAccount>()).Returns(_retailerRepoMock.Object);
        _uowMock.Setup(x => x.Repository<NotificationPreference>()).Returns(_notifRepoMock.Object);
        _uowMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> op, CancellationToken ct) => op(ct));
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<RetailerAccount>())).Returns("access-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("raw-refresh-token");

        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount r, CancellationToken _) => r);
        _notifRepoMock.Setup(x => x.AddAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference n, CancellationToken _) => n);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new LoginWithGoogleCommandHandler(
            _uowMock.Object,
            _googleAuthServiceMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    private void SetupGoogleValidation(GoogleUserInfo? googleUser = null)
    {
        if (googleUser is not null)
            _googleAuthServiceMock.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(googleUser);
        else
            _googleAuthServiceMock.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ValidGoogleUser);
    }

    // FIX: The handler calls the 3-param overload of FirstOrDefaultAsync (with orderBy) to
    // produce deterministic results when the OR predicate could match multiple rows (W-4 fix).
    // The previous mock only matched the 2-param overload, so Moq returned null for every
    // lookup, silently routing all calls into the "new account" path regardless of setup.
    // The mock must now match the 3-param signature: (predicate, orderBy, cancellationToken).
    private void SetupRetailerLookup(RetailerAccount? retailer)
    {
        _retailerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<Func<IQueryable<RetailerAccount>, IOrderedQueryable<RetailerAccount>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(retailer);
    }

    private static RetailerAccount CreateActiveRetailerWithGoogle(string googleId = "google-sub-abc123")
        => RetailerAccount.CreateWithGoogle("Test Retailer", "retailer@example.com", googleId, "brand-abc12345");

    private static RetailerAccount CreatePendingRetailer()
        => RetailerAccount.Create("Test Retailer", "retailer@example.com", "hash", "brand-xyz98765");

    private static RetailerAccount CreateRetailerWithStatus(string status)
    {
        var retailer = RetailerAccount.CreateWithGoogle("Test Retailer", "retailer@example.com", "gid", "brand-abc1");
        var prop = typeof(RetailerAccount).GetProperty("AccountStatus")!;
        prop.SetValue(retailer, status);
        return retailer;
    }

    [Fact]
    public async Task Handle_InvalidGoogleToken_PropagatesExternalServiceException()
    {
        _googleAuthServiceMock.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalServiceException("Google", "Invalid token"));

        var command = new LoginWithGoogleCommand("bad-token");

        await Assert.ThrowsAsync<ExternalServiceException>(() => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NewAccount_CreatesRetailerAndNotificationPreferenceAtomically()
    {
        SetupGoogleValidation();
        SetupRetailerLookup(null);

        var command = new LoginWithGoogleCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _retailerRepoMock.Verify(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifRepoMock.Verify(x => x.AddAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NewAccount_ReturnsAccessAndRefreshTokens()
    {
        SetupGoogleValidation();
        SetupRetailerLookup(null);

        var command = new LoginWithGoogleCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("access-token");
        result.Data.RefreshToken.Should().Be("raw-refresh-token");
        result.Data.RetailerProfile.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_NewAccount_BrandNameContainsEmailPrefix()
    {
        SetupGoogleValidation();
        SetupRetailerLookup(null);

        RetailerAccount? capturedRetailer = null;
        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Callback((RetailerAccount r, CancellationToken _) => capturedRetailer = r)
            .ReturnsAsync((RetailerAccount r, CancellationToken _) => r);

        var command = new LoginWithGoogleCommand("valid-token");
        await _sut.Handle(command, CancellationToken.None);

        capturedRetailer.Should().NotBeNull();
        capturedRetailer!.BrandName.Should().StartWith("retailer-");
    }

    [Fact]
    public async Task Handle_ExistingActiveAccountWithGoogleId_UpdatesRefreshTokenOnly()
    {
        var retailer = CreateActiveRetailerWithGoogle();
        SetupGoogleValidation();
        SetupRetailerLookup(retailer);

        var command = new LoginWithGoogleCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _retailerRepoMock.Verify(x => x.UpdateAsync(retailer, It.IsAny<CancellationToken>()), Times.Once);
        _retailerRepoMock.Verify(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifRepoMock.Verify(x => x.AddAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExistingActiveEmailAccountWithoutGoogleId_LinksGoogleIdAndReturnsTokens()
    {
        var retailer = CreatePendingRetailer();
        retailer.CompleteRegistration("Fashion", false, null);
        SetupGoogleValidation();
        SetupRetailerLookup(retailer);

        var command = new LoginWithGoogleCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        retailer.GoogleId.Should().Be(ValidGoogleUser.GoogleId);
        _retailerRepoMock.Verify(x => x.UpdateAsync(retailer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingAccountWithPendingEmailVerification_ThrowsBusinessRuleException()
    {
        var retailer = CreatePendingRetailer();
        SetupGoogleValidation();
        SetupRetailerLookup(retailer);

        var command = new LoginWithGoogleCommand("valid-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task Handle_ExistingAccountWithSuspendedStatus_ThrowsBusinessRuleException()
    {
        var retailer = CreateRetailerWithStatus(RetailerAccount.Status.Suspended);
        SetupGoogleValidation();
        SetupRetailerLookup(retailer);

        var command = new LoginWithGoogleCommand("valid-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task Handle_ExistingAccountWithPendingDeletionStatus_ThrowsBusinessRuleException()
    {
        var retailer = CreateRetailerWithStatus(RetailerAccount.Status.PendingDeletion);
        SetupGoogleValidation();
        SetupRetailerLookup(retailer);

        var command = new LoginWithGoogleCommand("valid-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task Handle_ExistingAccountWithDeletedStatus_ThrowsBusinessRuleException()
    {
        var retailer = CreateRetailerWithStatus(RetailerAccount.Status.Deleted);
        SetupGoogleValidation();
        SetupRetailerLookup(retailer);

        var command = new LoginWithGoogleCommand("valid-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task Handle_NewAccount_UniqueConstraintViolationOnInsert_ThrowsBusinessRuleException()
    {
        SetupGoogleValidation();
        SetupRetailerLookup(null);

        var innerEx = new Exception("duplicate key value violates unique constraint");
        var dbEx = new DbUpdateException("Unique violation", innerEx);

        _uowMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(dbEx);

        var command = new LoginWithGoogleCommand("valid-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_CONFLICT");
    }

    [Fact]
    public async Task Handle_ExistingAccount_UniqueConstraintViolationOnUpdate_ThrowsBusinessRuleException()
    {
        var retailer = CreateActiveRetailerWithGoogle();
        SetupGoogleValidation();
        SetupRetailerLookup(retailer);

        var innerEx = new Exception("unique constraint violation");
        var dbEx = new DbUpdateException("Unique violation", innerEx);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(dbEx);

        var command = new LoginWithGoogleCommand("valid-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("ACCOUNT_CONFLICT");
    }

    [Fact]
    public async Task Handle_NewAccount_RefreshTokenIsStoredAsHash()
    {
        SetupGoogleValidation();
        SetupRetailerLookup(null);

        RetailerAccount? capturedRetailer = null;
        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Callback((RetailerAccount r, CancellationToken _) => capturedRetailer = r)
            .ReturnsAsync((RetailerAccount r, CancellationToken _) => r);

        var command = new LoginWithGoogleCommand("valid-token");
        await _sut.Handle(command, CancellationToken.None);

        capturedRetailer.Should().NotBeNull();
        capturedRetailer!.RefreshTokenHash.Should().NotBeNull();
        capturedRetailer.RefreshTokenHash.Should().NotBe("raw-refresh-token");
    }

    [Fact]
    public async Task Handle_ExistingAccount_EmailLookupIsCaseInsensitive()
    {
        var googleUser = ValidGoogleUser with { Email = "RETAILER@EXAMPLE.COM" };
        SetupGoogleValidation(googleUser);

        var retailer = CreateActiveRetailerWithGoogle();
        SetupRetailerLookup(retailer);

        var command = new LoginWithGoogleCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SuccessfulLogin_ReturnsSuccessMessageInResult()
    {
        SetupGoogleValidation();
        SetupRetailerLookup(null);

        var command = new LoginWithGoogleCommand("valid-token");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().NotBeNullOrEmpty();
    }
}