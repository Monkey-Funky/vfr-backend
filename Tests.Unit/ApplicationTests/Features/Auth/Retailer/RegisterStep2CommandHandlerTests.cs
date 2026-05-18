using Application.Features.Auth.Commands.RegisterStep2;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Tests.Unit.Application.Features.Auth.Retailer;

public sealed class RegisterStep2CommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IFileStorageService> _fileStorageServiceMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<RegisterStep2CommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock = new();
    private readonly Mock<IRepository<NotificationPreference>> _notifPrefsRepoMock = new();
    private readonly RegisterStep2CommandHandler _sut;

    private const string ValidAccessToken = "access.token";
    private const string ValidRefreshToken = "refresh.token";
    private static readonly Guid AccountId = Guid.NewGuid();

    public RegisterStep2CommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<RetailerAccount>()).Returns(_retailerRepoMock.Object);
        _uowMock.Setup(x => x.Repository<NotificationPreference>()).Returns(_notifPrefsRepoMock.Object);
        _uowMock.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> op, CancellationToken ct) => op(ct));

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<RetailerAccount>())).Returns(ValidAccessToken);
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(ValidRefreshToken);

        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _notifPrefsRepoMock.Setup(x => x.AddAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference p, CancellationToken _) => p);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _sut = new RegisterStep2CommandHandler(
            _uowMock.Object,
            _tokenServiceMock.Object,
            _fileStorageServiceMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    private ClaimsPrincipal BuildValidPrincipal(Guid? accountId = null, string tokenType = "step", string step = "1")
    {
        var id = accountId ?? AccountId;
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("token_type", tokenType),
            new Claim("step", step),
            new Claim("temp_account_id", id.ToString())
        }));
    }

    private RetailerAccount CreatePendingAccount()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("password", workFactor: 4);
        return RetailerAccount.Create("John Doe", "john@example.com", hash, "JohnBrand");
    }

    private static RegisterStep2Command ValidCommand(Stream? logoStream = null, string? logoFileName = null) =>
        new(
            TempStepToken: "valid.step.token",
            BusinessType: "Fashion",
            Has3DModels: false,
            BrandLogoStream: logoStream,
            BrandLogoFileName: logoFileName,
            BrandLogoContentType: logoStream is not null ? "image/png" : null,
            BrandLogoSizeBytes: 0);

    [Fact]
    public async Task Handle_InvalidStepToken_ThrowsUnauthorizedAccessException()
    {
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>())).Returns((ClaimsPrincipal?)null);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*invalid or has expired*");
    }

    [Fact]
    public async Task Handle_TokenTypeNotStep_ThrowsUnauthorizedAccessException()
    {
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(tokenType: "access"));

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not a valid registration step token*");
    }

    [Fact]
    public async Task Handle_InvalidAccountIdClaim_ThrowsUnauthorizedAccessException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("token_type", "step"),
            new Claim("step", "1"),
            new Claim("temp_account_id", "not-a-guid")
        }));
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>())).Returns(principal);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*invalid account data*");
    }

    [Fact]
    public async Task Handle_EmptyGuidAccountId_ThrowsUnauthorizedAccessException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("token_type", "step"),
            new Claim("step", "1"),
            new Claim("temp_account_id", Guid.Empty.ToString())
        }));
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>())).Returns(principal);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WrongStepClaim_ThrowsUnauthorizedAccessException()
    {
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(step: "2"));

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not a valid Step 1 token*");
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsNotFoundException()
    {
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(AccountId));
        _retailerRepoMock.Setup(x => x.GetByIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount?)null);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AccountSoftDeleted_ThrowsNotFoundException()
    {
        var account = CreatePendingAccount();
        account.MarkAsDeleted();
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(AccountId));
        _retailerRepoMock.Setup(x => x.GetByIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AccountAlreadyActive_ThrowsBusinessRuleException()
    {
        var account = CreatePendingAccount();
        account.CompleteRegistration("Fashion", false, null);
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(AccountId));
        _retailerRepoMock.Setup(x => x.GetByIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("REGISTRATION_ALREADY_COMPLETED");
    }

    [Fact]
    public async Task Handle_ValidRequestWithoutLogo_CompletesRegistrationSuccessfully()
    {
        var account = CreatePendingAccount();
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(AccountId));
        _retailerRepoMock.Setup(x => x.GetByIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.AccessToken.Should().Be(ValidAccessToken);
        result.Data.RefreshToken.Should().Be(ValidRefreshToken);
        account.AccountStatus.Should().Be(RetailerAccount.Status.Active);
    }

    [Fact]
    public async Task Handle_ValidRequestWithLogo_UploadsLogoAndCompletesRegistration()
    {
        var logoUrl = "https://storage.example.com/brand-logos/logo.png";
        var account = CreatePendingAccount();
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(AccountId));
        _retailerRepoMock.Setup(x => x.GetByIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _fileStorageServiceMock.Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(logoUrl);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = ValidCommand(stream, "logo.png");

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        account.BrandLogoUrl.Should().Be(logoUrl);
        _fileStorageServiceMock.Verify(x => x.UploadAsync(stream, It.IsAny<string>(), "brand-logos", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TransactionFailsAfterLogoUpload_DeletesOrphanedS3Object()
    {
        var logoUrl = "https://storage.example.com/brand-logos/logo.png";
        var account = CreatePendingAccount();
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(AccountId));
        _retailerRepoMock.Setup(x => x.GetByIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _fileStorageServiceMock.Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(logoUrl);
        _fileStorageServiceMock.Setup(x => x.DeleteAsync(logoUrl, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB transaction failed"));

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = ValidCommand(stream, "logo.png");

        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _fileStorageServiceMock.Verify(x => x.DeleteAsync(logoUrl, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_TransactionFailsWithoutLogo_DoesNotCallDelete()
    {
        var account = CreatePendingAccount();
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(AccountId));
        _retailerRepoMock.Setup(x => x.GetByIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _uowMock.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB transaction failed"));

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _fileStorageServiceMock.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidRequest_SeedsNotificationPreference()
    {
        var account = CreatePendingAccount();
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(AccountId));
        _retailerRepoMock.Setup(x => x.GetByIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _notifPrefsRepoMock.Verify(x => x.AddAsync(It.IsAny<NotificationPreference>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_ResponseHas900SecondExpiry()
    {
        var account = CreatePendingAccount();
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(AccountId));
        _retailerRepoMock.Setup(x => x.GetByIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.Data!.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Handle_ValidRequest_RefreshTokenExpiresIn7Days()
    {
        var account = CreatePendingAccount();
        _tokenServiceMock.Setup(x => x.ValidateTempStepToken(It.IsAny<string>()))
            .Returns(BuildValidPrincipal(AccountId));
        _retailerRepoMock.Setup(x => x.GetByIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        account.RefreshTokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        account.IsRememberMeSession.Should().BeFalse();
    }
}