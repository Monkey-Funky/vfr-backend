using Application.Features.Customer.Auth.Commands.Register;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Customer.Auth;

public sealed class RegisterCustomerCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ILogger<RegisterCustomerCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<CustomerAccount>> _customerRepoMock = new();
    private readonly RegisterCustomerCommandHandler _sut;

    private const string ValidEmail = "newcustomer@example.com";
    private const string ValidPassword = "Password123!";
    private const string ValidFullName = "New Customer";

    public RegisterCustomerCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<CustomerAccount>()).Returns(_customerRepoMock.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _customerRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerAccount c, CancellationToken _) => c);

        _tokenServiceMock.Setup(x => x.GenerateTempStepToken(It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns("step-token-jwt");

        _sut = new RegisterCustomerCommandHandler(
            _uowMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    private void SetupEmailExists(bool exists)
    {
        _customerRepoMock.Setup(x => x.AnyAsync(
                It.IsAny<Expression<Func<CustomerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists);
    }

    [Fact]
    public async Task Handle_EmailAlreadyExists_ReturnsGenericSuccessToPreventEnumeration()
    {
        SetupEmailExists(true);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(string.Empty);
    }

    [Fact]
    public async Task Handle_EmailAlreadyExists_DoesNotCreateNewAccount()
    {
        SetupEmailExists(true);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);
        await _sut.Handle(command, CancellationToken.None);

        _customerRepoMock.Verify(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmailAlreadyExists_PerformsTimingEqualizationHash()
    {
        SetupEmailExists(true);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _sut.Handle(command, CancellationToken.None);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_NewEmail_CreatesAccountWithPendingEmailVerificationStatus()
    {
        SetupEmailExists(false);

        CustomerAccount? capturedAccount = null;
        _customerRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Callback((CustomerAccount c, CancellationToken _) => capturedAccount = c)
            .ReturnsAsync((CustomerAccount c, CancellationToken _) => c);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);
        await _sut.Handle(command, CancellationToken.None);

        capturedAccount.Should().NotBeNull();
        capturedAccount!.Status.Should().Be(Domain.Enums.Customer.CustomerStatus.PendingEmailVerification);
    }

    [Fact]
    public async Task Handle_NewEmail_StoresHashedPasswordNotPlaintext()
    {
        SetupEmailExists(false);

        CustomerAccount? capturedAccount = null;
        _customerRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Callback((CustomerAccount c, CancellationToken _) => capturedAccount = c)
            .ReturnsAsync((CustomerAccount c, CancellationToken _) => c);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);
        await _sut.Handle(command, CancellationToken.None);

        capturedAccount!.PasswordHash.Should().NotBeNull();
        capturedAccount.PasswordHash.Should().NotBe(ValidPassword);
        BCrypt.Net.BCrypt.Verify(ValidPassword, capturedAccount.PasswordHash!).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NewEmail_ReturnsStepToken()
    {
        SetupEmailExists(false);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("step-token-jwt");
    }

    [Fact]
    public async Task Handle_NewEmail_GeneratesStep1Token()
    {
        SetupEmailExists(false);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);
        await _sut.Handle(command, CancellationToken.None);

        _tokenServiceMock.Verify(x => x.GenerateTempStepToken(It.IsAny<Guid>(), 1), Times.Once);
    }

    [Fact]
    public async Task Handle_NewEmail_PersistsAccountToDatabase()
    {
        SetupEmailExists(false);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);
        await _sut.Handle(command, CancellationToken.None);

        _customerRepoMock.Verify(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NewEmail_NormalizesEmailToLowercase()
    {
        SetupEmailExists(false);

        CustomerAccount? capturedAccount = null;
        _customerRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Callback((CustomerAccount c, CancellationToken _) => capturedAccount = c)
            .ReturnsAsync((CustomerAccount c, CancellationToken _) => c);

        var command = new RegisterCustomerCommand(ValidFullName, "  NEWCUSTOMER@EXAMPLE.COM  ", ValidPassword);
        await _sut.Handle(command, CancellationToken.None);

        capturedAccount!.Email.Should().Be("newcustomer@example.com");
    }

    [Fact]
    public async Task Handle_NewEmail_StoresCorrectFullName()
    {
        SetupEmailExists(false);

        CustomerAccount? capturedAccount = null;
        _customerRepoMock.Setup(x => x.AddAsync(It.IsAny<CustomerAccount>(), It.IsAny<CancellationToken>()))
            .Callback((CustomerAccount c, CancellationToken _) => capturedAccount = c)
            .ReturnsAsync((CustomerAccount c, CancellationToken _) => c);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);
        await _sut.Handle(command, CancellationToken.None);

        capturedAccount!.FullName.Should().Be(ValidFullName);
    }

    [Fact]
    public async Task Handle_UniqueConstraintViolationOnSave_ThrowsConflictException()
    {
        SetupEmailExists(false);

        var innerEx = new Exception("duplicate key value violates unique constraint");
        var dbEx = new DbUpdateException("Unique violation", innerEx);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(dbEx);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UniqueConstraintViolation_ThrowsConflictExceptionWithEmailMessage()
    {
        SetupEmailExists(false);

        var innerEx = new Exception("unique constraint");
        var dbEx = new DbUpdateException("Unique violation", innerEx);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(dbEx);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _sut.Handle(command, CancellationToken.None));
        ex.Message.Should().Contain("email");
    }

    [Fact]
    public async Task Handle_NonUniqueDbUpdateException_Propagates()
    {
        SetupEmailExists(false);

        var innerEx = new Exception("some other db error");
        var dbEx = new DbUpdateException("Generic error", innerEx);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(dbEx);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);

        await Assert.ThrowsAsync<DbUpdateException>(() => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NewEmail_ReturnsSuccessMessageInResult()
    {
        SetupEmailExists(false);

        var command = new RegisterCustomerCommand(ValidFullName, ValidEmail, ValidPassword);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().NotBeNullOrEmpty();
    }
}