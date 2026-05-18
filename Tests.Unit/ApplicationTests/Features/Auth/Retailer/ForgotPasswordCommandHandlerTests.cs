using Application.Features.Auth.Commands.ForgotPassword;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Auth.Retailer;

public sealed class ForgotPasswordCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<ForgotPasswordCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock = new();
    private readonly ForgotPasswordCommandHandler _sut;

    public ForgotPasswordCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<RetailerAccount>()).Returns(_retailerRepoMock.Object);
        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new ForgotPasswordCommandHandler(_uowMock.Object, _cacheServiceMock.Object, _emailServiceMock.Object, _loggerMock.Object);
    }

    private void SetupAccountQuery(RetailerAccount? account) =>
        _retailerRepoMock.Setup(x => x.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<RetailerAccount, bool>>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

    private static RetailerAccount CreateActiveRetailer(string email = "retailer@example.com")
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("password", workFactor: 4);
        var account = RetailerAccount.Create("John Doe", email, hash, "TestBrand");
        account.CompleteRegistration("Fashion", false, null);
        return account;
    }

    [Fact]
    public async Task Handle_EmailNotFound_ReturnsSilentSuccess()
    {
        SetupAccountQuery(null);

        var result = await _sut.Handle(new ForgotPasswordCommand("notfound@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        result.Message.Should().Contain("If an account with that email exists");
    }

    [Fact]
    public async Task Handle_EmailNotFound_DoesNotSendEmail()
    {
        SetupAccountQuery(null);

        await _sut.Handle(new ForgotPasswordCommand("notfound@example.com"), CancellationToken.None);

        _emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmailNotFound_DoesNotCacheOtp()
    {
        SetupAccountQuery(null);

        await _sut.Handle(new ForgotPasswordCommand("notfound@example.com"), CancellationToken.None);

        _cacheServiceMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AccountFound_CachesHashedOtp()
    {
        var account = CreateActiveRetailer();
        SetupAccountQuery(account);

        await _sut.Handle(new ForgotPasswordCommand("retailer@example.com"), CancellationToken.None);

        _cacheServiceMock.Verify(x => x.SetAsync(
            It.Is<string>(k => k.Contains("pwd_reset:")),
            It.Is<string>(h => h.StartsWith("$2")),
            It.Is<TimeSpan>(t => t == TimeSpan.FromMinutes(15)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AccountFound_SendsOtpEmail()
    {
        var account = CreateActiveRetailer("retailer@example.com");
        SetupAccountQuery(account);

        await _sut.Handle(new ForgotPasswordCommand("retailer@example.com"), CancellationToken.None);

        _emailServiceMock.Verify(x => x.SendEmailAsync(
            account.Email,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AccountFound_ReturnsSilentSuccess()
    {
        var account = CreateActiveRetailer();
        SetupAccountQuery(account);

        var result = await _sut.Handle(new ForgotPasswordCommand("retailer@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        result.Message.Should().Contain("If an account with that email exists");
    }

    [Fact]
    public async Task Handle_AccountFound_CacheKeyContainsLowerCaseEmail()
    {
        var account = CreateActiveRetailer("Retailer@Example.COM");
        SetupAccountQuery(account);

        await _sut.Handle(new ForgotPasswordCommand("Retailer@Example.COM"), CancellationToken.None);

        _cacheServiceMock.Verify(x => x.SetAsync(
            It.Is<string>(k => k == "pwd_reset:retailer@example.com"),
            It.IsAny<string>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AccountFound_StoredHashIsNotRawOtp()
    {
        var account = CreateActiveRetailer();
        SetupAccountQuery(account);

        string? capturedHashedOtp = null;
        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, TimeSpan, CancellationToken>((_, hash, _, _) => capturedHashedOtp = hash)
            .Returns(Task.CompletedTask);

        await _sut.Handle(new ForgotPasswordCommand("retailer@example.com"), CancellationToken.None);

        capturedHashedOtp.Should().NotBeNullOrEmpty();
        capturedHashedOtp.Should().StartWith("$2");
    }

    [Fact]
    public async Task Handle_BothFoundAndNotFoundPaths_ReturnIdenticalMessage()
    {
        SetupAccountQuery(null);
        var notFoundResult = await _sut.Handle(new ForgotPasswordCommand("ghost@example.com"), CancellationToken.None);

        var account = CreateActiveRetailer();
        SetupAccountQuery(account);
        var foundResult = await _sut.Handle(new ForgotPasswordCommand("retailer@example.com"), CancellationToken.None);

        notFoundResult.Message.Should().Be(foundResult.Message);
    }

    [Fact]
    public async Task Handle_OtpTtlIs15Minutes()
    {
        var account = CreateActiveRetailer();
        SetupAccountQuery(account);

        TimeSpan? capturedTtl = null;
        _cacheServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, TimeSpan, CancellationToken>((_, _, ttl, _) => capturedTtl = ttl)
            .Returns(Task.CompletedTask);

        await _sut.Handle(new ForgotPasswordCommand("retailer@example.com"), CancellationToken.None);

        capturedTtl.Should().Be(TimeSpan.FromMinutes(15));
    }
}