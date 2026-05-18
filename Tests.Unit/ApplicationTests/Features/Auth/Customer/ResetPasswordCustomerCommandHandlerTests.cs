using Application.Features.Customer.Auth.Commands.ResetPassword;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Customer;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Customer.Auth;

public sealed class ResetPasswordCustomerCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ILogger<ResetPasswordCustomerCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<CustomerAccount>> _customerRepoMock = new();
    private readonly ResetPasswordCustomerCommandHandler _sut;

    private const string ValidEmail = "customer@example.com";
    private const string ValidOtp = "654321";
    private const string ValidNewPassword = "NewPassword456!";
    private static readonly string ValidOtpHash = BCrypt.Net.BCrypt.HashPassword(ValidOtp, workFactor: 4);
    private static readonly string CacheKey = $"customer_pwd_reset:{ValidEmail}";

    public ResetPasswordCustomerCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<CustomerAccount>()).Returns(_customerRepoMock.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _customerRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new ResetPasswordCustomerCommandHandler(
            _uowMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    private CustomerAccount CreateActiveCustomer()
    {
        var customer = CustomerAccount.Create("Test Customer", ValidEmail, "old-hash");
        customer.MarkEmailVerified();
        return customer;
    }

    private void SetupCustomerLookup(CustomerAccount? customer)
    {
        _customerRepoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<CustomerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
    }

    private void SetupCacheOtp(string? hashedOtp)
    {
        _cacheServiceMock.Setup(x => x.GetAsync<string>(CacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hashedOtp);
    }

    [Fact]
    public async Task Handle_CustomerNotFound_ThrowsBusinessRuleException()
    {
        SetupCustomerLookup(null);

        var command = new ResetPasswordCustomerCommand(ValidEmail, ValidOtp, ValidNewPassword);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("INVALID_OTP");
    }

    [Fact]
    public async Task Handle_CustomerNotActive_ThrowsBusinessRuleException()
    {
        var customer = CustomerAccount.Create("Test Customer", ValidEmail, "hash");
        var prop = typeof(CustomerAccount).GetProperty("Status")!;
        prop.SetValue(customer, CustomerStatus.Suspended);
        SetupCustomerLookup(customer);

        var command = new ResetPasswordCustomerCommand(ValidEmail, ValidOtp, ValidNewPassword);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("INVALID_OTP");
    }

    [Fact]
    public async Task Handle_CustomerPendingEmailVerification_ThrowsBusinessRuleException()
    {
        var customer = CustomerAccount.Create("Test Customer", ValidEmail, "hash");
        SetupCustomerLookup(customer);

        var command = new ResetPasswordCustomerCommand(ValidEmail, ValidOtp, ValidNewPassword);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("INVALID_OTP");
    }

    [Fact]
    public async Task Handle_OtpNotInCache_ThrowsBusinessRuleException()
    {
        SetupCustomerLookup(CreateActiveCustomer());
        _cacheServiceMock.Setup(x => x.GetAsync<string>(CacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var command = new ResetPasswordCustomerCommand(ValidEmail, ValidOtp, ValidNewPassword);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("INVALID_OTP");
    }

    [Fact]
    public async Task Handle_OtpExpiredInCache_ThrowsBusinessRuleException()
    {
        SetupCustomerLookup(CreateActiveCustomer());
        _cacheServiceMock.Setup(x => x.GetAsync<string>(
                $"customer_pwd_reset:{ValidEmail}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var command = new ResetPasswordCustomerCommand(ValidEmail, ValidOtp, ValidNewPassword);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("INVALID_OTP");
    }

    [Fact]
    public async Task Handle_WrongOtpCode_ThrowsBusinessRuleException()
    {
        SetupCustomerLookup(CreateActiveCustomer());
        SetupCacheOtp(ValidOtpHash);

        var command = new ResetPasswordCustomerCommand(ValidEmail, "000000", ValidNewPassword);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Code.Should().Be("INVALID_OTP");
    }

    [Fact]
    public async Task Handle_ValidOtp_UpdatesPasswordWithNewHash()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);
        SetupCacheOtp(ValidOtpHash);

        var command = new ResetPasswordCustomerCommand(ValidEmail, ValidOtp, ValidNewPassword);
        await _sut.Handle(command, CancellationToken.None);

        customer.PasswordHash.Should().NotBeNull();
        customer.PasswordHash.Should().NotBe(ValidNewPassword);
        BCrypt.Net.BCrypt.Verify(ValidNewPassword, customer.PasswordHash!).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidOtp_RevokesAllRefreshTokens()
    {
        var customer = CreateActiveCustomer();
        customer.UpdateRefreshToken("old-hash", DateTime.UtcNow.AddDays(7), false);
        SetupCustomerLookup(customer);
        SetupCacheOtp(ValidOtpHash);

        var command = new ResetPasswordCustomerCommand(ValidEmail, ValidOtp, ValidNewPassword);
        await _sut.Handle(command, CancellationToken.None);

        customer.RefreshTokenHash.Should().BeNull();
        customer.RefreshTokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidOtp_RemovesOtpFromCache()
    {
        SetupCustomerLookup(CreateActiveCustomer());
        SetupCacheOtp(ValidOtpHash);

        var command = new ResetPasswordCustomerCommand(ValidEmail, ValidOtp, ValidNewPassword);
        await _sut.Handle(command, CancellationToken.None);

        _cacheServiceMock.Verify(x => x.RemoveAsync(CacheKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidOtp_PersistsChangesToDatabase()
    {
        var customer = CreateActiveCustomer();
        SetupCustomerLookup(customer);
        SetupCacheOtp(ValidOtpHash);

        var command = new ResetPasswordCustomerCommand(ValidEmail, ValidOtp, ValidNewPassword);
        await _sut.Handle(command, CancellationToken.None);

        _customerRepoMock.Verify(x => x.UpdateAsync(customer, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidOtp_ReturnsSuccess()
    {
        SetupCustomerLookup(CreateActiveCustomer());
        SetupCacheOtp(ValidOtpHash);

        var command = new ResetPasswordCustomerCommand(ValidEmail, ValidOtp, ValidNewPassword);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidOtp_EmailNormalizationUsedForCacheKeyLookup()
    {
        SetupCustomerLookup(CreateActiveCustomer());

        _cacheServiceMock.Setup(x => x.GetAsync<string>(CacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidOtpHash);

        var command = new ResetPasswordCustomerCommand("  CUSTOMER@EXAMPLE.COM  ", ValidOtp, ValidNewPassword);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _cacheServiceMock.Verify(x => x.GetAsync<string>(CacheKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidOtp_OldPasswordNoLongerValid()
    {
        const string oldPassword = "OldPassword123!";
        var oldHash = BCrypt.Net.BCrypt.HashPassword(oldPassword, workFactor: 4);
        var customer = CustomerAccount.Create("Test Customer", ValidEmail, oldHash);
        customer.MarkEmailVerified();
        SetupCustomerLookup(customer);
        SetupCacheOtp(ValidOtpHash);

        var command = new ResetPasswordCustomerCommand(ValidEmail, ValidOtp, ValidNewPassword);
        await _sut.Handle(command, CancellationToken.None);

        BCrypt.Net.BCrypt.Verify(oldPassword, customer.PasswordHash!).Should().BeFalse();
    }
}