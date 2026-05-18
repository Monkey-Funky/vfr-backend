using Application.Features.Auth.Commands.Login;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using Shared.DTOs;

namespace Tests.Unit.Application.Features.Auth.Retailer;

public sealed class LoginCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ILogger<LoginCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock = new();
    private readonly LoginCommandHandler _sut;

    private const string ValidPassword = "ValidPassword123!";
    private const string ValidAccessToken = "access.token.value";
    private const string ValidRefreshToken = "raw_refresh_token_value";
    private static readonly string ValidPasswordHash = BCrypt.Net.BCrypt.HashPassword(ValidPassword, workFactor: 4);

    public LoginCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<RetailerAccount>()).Returns(_retailerRepoMock.Object);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<RetailerAccount>())).Returns(ValidAccessToken);
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(ValidRefreshToken);

        _sut = new LoginCommandHandler(_uowMock.Object, _tokenServiceMock.Object, _loggerMock.Object);
    }

    private static RetailerAccount CreateActiveRetailer(string email = "retailer@example.com", string? passwordHash = null)
    {
        var hash = passwordHash ?? ValidPasswordHash;
        var account = RetailerAccount.Create("John Doe", email, hash, "TestBrand");
        account.CompleteRegistration("Fashion", false, null);
        return account;
    }

    private static RetailerAccount CreateRetailerWithStatus(string status, string email = "retailer@example.com")
    {
        var account = RetailerAccount.Create("John Doe", email, ValidPasswordHash, "TestBrand");
        if (status == RetailerAccount.Status.Active)
            account.CompleteRegistration("Fashion", false, null);
        else if (status == RetailerAccount.Status.PendingDeletion)
        {
            account.CompleteRegistration("Fashion", false, null);
            account.MarkPendingDeletion();
        }
        else
        {
            typeof(RetailerAccount).GetProperty(nameof(RetailerAccount.AccountStatus))!
                .SetValue(account, status);
        }
        return account;
    }

    private void SetupRetailerQuery(RetailerAccount? account)
    {
        _retailerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<RetailerAccount, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
    }

    [Fact]
    public async Task Handle_EmailNotFound_ThrowsUnauthorizedAccessException()
    {
        SetupRetailerQuery(null);
        var command = new LoginCommand("notfound@example.com", "anyPassword", false);

        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task Handle_AccountLockedOut_ThrowsUnauthorizedAccessExceptionWithRemainingMinutes()
    {
        var account = CreateActiveRetailer();
        for (int i = 0; i < 10; i++)
            account.IncrementFailedLoginCount();

        SetupRetailerQuery(account);
        var command = new LoginCommand("retailer@example.com", ValidPassword, false);

        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<UnauthorizedAccessException>();
        ex.Which.Message.Should().Contain("temporarily locked");
        ex.Which.Message.Should().Contain("minute");
    }

    [Fact]
    public async Task Handle_AccountPendingEmailVerification_ThrowsBusinessRuleException()
    {
        var account = RetailerAccount.Create("John Doe", "retailer@example.com", ValidPasswordHash, "TestBrand");
        SetupRetailerQuery(account);
        var command = new LoginCommand("retailer@example.com", ValidPassword, false);

        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("EMAIL_NOT_VERIFIED");
    }

    [Fact]
    public async Task Handle_AccountSuspended_ThrowsBusinessRuleException()
    {
        var account = CreateRetailerWithStatus(RetailerAccount.Status.Suspended);
        SetupRetailerQuery(account);
        var command = new LoginCommand("retailer@example.com", ValidPassword, false);

        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("ACCOUNT_SUSPENDED");
    }

    [Fact]
    public async Task Handle_AccountPendingDeletion_ThrowsBusinessRuleException()
    {
        var account = CreateRetailerWithStatus(RetailerAccount.Status.PendingDeletion);
        SetupRetailerQuery(account);
        var command = new LoginCommand("retailer@example.com", ValidPassword, false);

        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task Handle_AccountDeleted_ThrowsBusinessRuleException()
    {
        var account = CreateRetailerWithStatus(RetailerAccount.Status.Deleted);
        SetupRetailerQuery(account);
        var command = new LoginCommand("retailer@example.com", ValidPassword, false);

        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task Handle_LockoutCheckedBeforeStatus_LockedPendingAccountThrowsLockoutError()
    {
        var account = RetailerAccount.Create("John Doe", "retailer@example.com", ValidPasswordHash, "TestBrand");
        for (int i = 0; i < 10; i++)
            account.IncrementFailedLoginCount();

        SetupRetailerQuery(account);
        var command = new LoginCommand("retailer@example.com", ValidPassword, false);

        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<UnauthorizedAccessException>();
        ex.Which.Message.Should().Contain("temporarily locked");
    }

    [Fact]
    public async Task Handle_WrongPassword_ThrowsUnauthorizedAndIncrementsFailedCount()
    {
        var account = CreateActiveRetailer();
        var initialFailedCount = account.AccessFailedCount;
        SetupRetailerQuery(account);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("retailer@example.com", "WrongPassword!", false);

        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid email or password.");
        account.AccessFailedCount.Should().Be(initialFailedCount + 1);
    }

    [Fact]
    public async Task Handle_WrongPasswordPersistsFailedCount()
    {
        var account = CreateActiveRetailer();
        SetupRetailerQuery(account);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("retailer@example.com", "WrongPassword!", false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.Handle(command, CancellationToken.None));

        _retailerRepoMock.Verify(x => x.UpdateAsync(account, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NinthFailedAttempt_DoesNotLockAccount()
    {
        var account = CreateActiveRetailer();
        for (int i = 0; i < 8; i++)
            account.IncrementFailedLoginCount();

        SetupRetailerQuery(account);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("retailer@example.com", "WrongPassword!", false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.Handle(command, CancellationToken.None));

        account.IsLockedOut().Should().BeFalse();
    }

    [Fact]
    public async Task Handle_TenthFailedAttempt_LocksAccount()
    {
        var account = CreateActiveRetailer();
        for (int i = 0; i < 9; i++)
            account.IncrementFailedLoginCount();

        SetupRetailerQuery(account);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("retailer@example.com", "WrongPassword!", false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.Handle(command, CancellationToken.None));

        account.IsLockedOut().Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidCredentials_RememberMeFalse_Returns7DayToken()
    {
        var account = CreateActiveRetailer();
        SetupRetailerQuery(account);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("retailer@example.com", ValidPassword, RememberMe: false);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.AccessToken.Should().Be(ValidAccessToken);
        result.Data.RefreshToken.Should().Be(ValidRefreshToken);
        account.RefreshTokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        account.IsRememberMeSession.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidCredentials_RememberMeTrue_Returns30DayToken()
    {
        var account = CreateActiveRetailer();
        SetupRetailerQuery(account);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("retailer@example.com", ValidPassword, RememberMe: true);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        account.RefreshTokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
        account.IsRememberMeSession.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidCredentials_ResetsFailedLoginCount()
    {
        var account = CreateActiveRetailer();
        for (int i = 0; i < 5; i++)
            account.IncrementFailedLoginCount();

        SetupRetailerQuery(account);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("retailer@example.com", ValidPassword, false);

        await _sut.Handle(command, CancellationToken.None);

        account.AccessFailedCount.Should().Be(0);
        account.IsLockedOut().Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidCredentials_PersistsRefreshToken()
    {
        var account = CreateActiveRetailer();
        SetupRetailerQuery(account);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("retailer@example.com", ValidPassword, false);

        await _sut.Handle(command, CancellationToken.None);

        _retailerRepoMock.Verify(x => x.UpdateAsync(account, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        account.RefreshTokenHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_ValidCredentials_RetailerProfileIncludedInResponse()
    {
        var account = CreateActiveRetailer();
        SetupRetailerQuery(account);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("retailer@example.com", ValidPassword, false);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Data!.RetailerProfile.Should().NotBeNull();
        result.Data.RetailerProfile.Email.Should().Be(account.Email);
        result.Data.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Handle_EmailIsCaseInsensitive_FindsRetailer()
    {
        var account = CreateActiveRetailer("retailer@example.com");
        SetupRetailerQuery(account);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new LoginCommand("RETAILER@EXAMPLE.COM", ValidPassword, false);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}