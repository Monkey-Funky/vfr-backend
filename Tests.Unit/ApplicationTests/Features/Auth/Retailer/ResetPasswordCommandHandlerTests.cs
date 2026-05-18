using Application.Features.Auth.Commands.ResetPassword;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Auth.Retailer;

public sealed class ResetPasswordCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ILogger<ResetPasswordCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock = new();
    private readonly ResetPasswordCommandHandler _sut;

    private const string ValidOtp = "123456";
    private const string ValidEmail = "retailer@example.com";
    private const string NewPassword = "NewSecurePassword123!";
    private static readonly string HashedOtp = BCrypt.Net.BCrypt.HashPassword(ValidOtp, workFactor: 4);
    private static readonly string CacheKey = $"pwd_reset:{ValidEmail}";

    public ResetPasswordCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<RetailerAccount>()).Returns(_retailerRepoMock.Object);
        _retailerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _sut = new ResetPasswordCommandHandler(_uowMock.Object, _cacheServiceMock.Object, _loggerMock.Object);
    }

    private void SetupAccountQuery(RetailerAccount? account) =>
        _retailerRepoMock.Setup(x => x.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<RetailerAccount, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

    private void SetupCachedOtp(string? hashedOtp) =>
        _cacheServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hashedOtp);

    private static RetailerAccount CreateActiveRetailer()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!", workFactor: 4);
        var account = RetailerAccount.Create("John Doe", ValidEmail, hash, "TestBrand");
        account.CompleteRegistration("Fashion", false, null);
        return account;
    }

    private static ResetPasswordCommand ValidCommand() =>
        new(ValidEmail, ValidOtp, NewPassword);

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsBusinessRuleException()
    {
        SetupAccountQuery(null);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INVALID_OTP");
    }

    [Fact]
    public async Task Handle_AccountInactive_ThrowsBusinessRuleException()
    {
        SetupAccountQuery(null);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INVALID_OTP");
    }

    [Fact]
    public async Task Handle_OtpNotInCache_ThrowsBusinessRuleException()
    {
        var account = CreateActiveRetailer();
        SetupAccountQuery(account);
        SetupCachedOtp(null);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INVALID_OTP");
        ex.Which.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task Handle_WrongOtpCode_ThrowsBusinessRuleException()
    {
        var account = CreateActiveRetailer();
        SetupAccountQuery(account);
        SetupCachedOtp(HashedOtp);

        var command = new ResetPasswordCommand(ValidEmail, "999999", NewPassword);

        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INVALID_OTP");
        ex.Which.Message.Should().Contain("incorrect");
    }

    [Fact]
    public async Task Handle_ValidOtp_UpdatesPasswordHash()
    {
        var account = CreateActiveRetailer();
        var originalHash = account.PasswordHash;
        SetupAccountQuery(account);
        SetupCachedOtp(HashedOtp);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        account.PasswordHash.Should().NotBe(originalHash);
        BCrypt.Net.BCrypt.Verify(NewPassword, account.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidOtp_RevokesAllRefreshTokens()
    {
        var account = CreateActiveRetailer();
        var hash = BCrypt.Net.BCrypt.HashPassword("sometoken", workFactor: 4);
        account.UpdateRefreshToken(hash, DateTime.UtcNow.AddDays(7), false);
        SetupAccountQuery(account);
        SetupCachedOtp(HashedOtp);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        account.RefreshTokenHash.Should().BeNull();
        account.RefreshTokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidOtp_DeletesOtpFromCache()
    {
        var account = CreateActiveRetailer();
        SetupAccountQuery(account);
        SetupCachedOtp(HashedOtp);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _cacheServiceMock.Verify(x => x.RemoveAsync(
            It.Is<string>(k => k.Contains("pwd_reset:")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidOtp_DeletesOtpBeforeDbSave()
    {
        var account = CreateActiveRetailer();
        SetupAccountQuery(account);
        SetupCachedOtp(HashedOtp);

        var callOrder = new List<string>();
        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("cache_remove"))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("db_save"))
            .ReturnsAsync(1);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        callOrder.Should().ContainInOrder("cache_remove", "db_save");
    }

    [Fact]
    public async Task Handle_ValidOtp_PersistsAccountChanges()
    {
        var account = CreateActiveRetailer();
        SetupAccountQuery(account);
        SetupCachedOtp(HashedOtp);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _retailerRepoMock.Verify(x => x.UpdateAsync(account, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidOtp_ReturnsSuccess()
    {
        var account = CreateActiveRetailer();
        SetupAccountQuery(account);
        SetupCachedOtp(HashedOtp);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        result.Message.Should().Contain("reset successfully");
    }

    [Fact]
    public async Task Handle_EmailNormalized_CacheKeyUsesLowerCase()
    {
        var account = CreateActiveRetailer();
        SetupAccountQuery(account);
        SetupCachedOtp(HashedOtp);

        string? capturedKey = null;
        _cacheServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => capturedKey = key)
            .ReturnsAsync(HashedOtp);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        capturedKey.Should().Be(CacheKey.ToLowerInvariant());
    }
}