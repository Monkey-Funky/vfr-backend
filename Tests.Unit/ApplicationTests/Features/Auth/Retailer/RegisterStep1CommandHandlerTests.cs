using Application.Features.Auth.Commands.RegisterStep1;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Auth.Retailer;

public sealed class RegisterStep1CommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ILogger<RegisterStep1CommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock = new();
    private readonly RegisterStep1CommandHandler _sut;

    private const string StepToken = "step.token.value";

    public RegisterStep1CommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<RetailerAccount>()).Returns(_retailerRepoMock.Object);
        _tokenServiceMock.Setup(x => x.GenerateTempStepToken(It.IsAny<Guid>(), It.IsAny<int>())).Returns(StepToken);

        _sut = new RegisterStep1CommandHandler(_uowMock.Object, _tokenServiceMock.Object, _loggerMock.Object);
    }

    private void SetupEmailExists(bool exists) =>
        _retailerRepoMock
            .SetupSequence(x => x.AnyAsync(It.IsAny<Expression<Func<RetailerAccount, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exists);

    private void SetupEmailAndBrandExist(bool emailExists, bool brandExists) =>
        _retailerRepoMock
            .SetupSequence(x => x.AnyAsync(It.IsAny<Expression<Func<RetailerAccount, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailExists)
            .ReturnsAsync(brandExists);

    private static RegisterStep1Command ValidCommand() =>
        new("John Doe", "newretailer@example.com", "StrongPassword123!", "UniqueBrand");

    [Fact]
    public async Task Handle_EmailAlreadyExists_ThrowsConflictException()
    {
        SetupEmailExists(true);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.Message.Should().Contain("email");
    }

    [Fact]
    public async Task Handle_BrandNameAlreadyExists_ThrowsConflictException()
    {
        SetupEmailAndBrandExist(false, true);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.Message.Should().Contain("brand name");
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesAccountWithPendingStatus()
    {
        SetupEmailAndBrandExist(false, false);
        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount a, CancellationToken _) => a);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        RetailerAccount? capturedAccount = null;
        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Callback<RetailerAccount, CancellationToken>((a, _) => capturedAccount = a)
            .ReturnsAsync((RetailerAccount a, CancellationToken _) => a);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        capturedAccount.Should().NotBeNull();
        capturedAccount!.AccountStatus.Should().Be(RetailerAccount.Status.PendingEmailVerification);
        capturedAccount.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidRequest_HashesPassword()
    {
        SetupEmailAndBrandExist(false, false);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        RetailerAccount? capturedAccount = null;
        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Callback<RetailerAccount, CancellationToken>((a, _) => capturedAccount = a)
            .ReturnsAsync((RetailerAccount a, CancellationToken _) => a);

        var command = ValidCommand();
        await _sut.Handle(command, CancellationToken.None);

        capturedAccount!.PasswordHash.Should().NotBe(command.Password);
        BCrypt.Net.BCrypt.Verify(command.Password, capturedAccount.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsStepToken()
    {
        SetupEmailAndBrandExist(false, false);
        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount a, CancellationToken _) => a);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(StepToken);
    }

    [Fact]
    public async Task Handle_ValidRequest_GeneratesStepTokenWithStep1()
    {
        SetupEmailAndBrandExist(false, false);
        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount a, CancellationToken _) => a);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _tokenServiceMock.Verify(x => x.GenerateTempStepToken(It.IsAny<Guid>(), 1), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsAccount()
    {
        SetupEmailAndBrandExist(false, false);
        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount a, CancellationToken _) => a);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _retailerRepoMock.Verify(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UniqueConstraintViolation_ThrowsConflictException()
    {
        SetupEmailAndBrandExist(false, false);
        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount a, CancellationToken _) => a);

        var innerEx = new Exception("duplicate key value violates unique constraint");
        var dbEx = new DbUpdateException("unique constraint violation", innerEx);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(dbEx);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_NonUniqueDbException_Rethrows()
    {
        SetupEmailAndBrandExist(false, false);
        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount a, CancellationToken _) => a);

        var dbEx = new DbUpdateException("some other db error", new Exception("disk error"));
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(dbEx);

        Func<Task> act = async () => await _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_SetsCorrectEmailAndBrandName()
    {
        SetupEmailAndBrandExist(false, false);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        RetailerAccount? capturedAccount = null;
        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Callback<RetailerAccount, CancellationToken>((a, _) => capturedAccount = a)
            .ReturnsAsync((RetailerAccount a, CancellationToken _) => a);

        var command = ValidCommand();
        await _sut.Handle(command, CancellationToken.None);

        capturedAccount!.Email.Should().Be(command.Email);
        capturedAccount.BrandName.Should().Be(command.BrandName);
        capturedAccount.FullName.Should().Be(command.FullName);
    }

    [Fact]
    public async Task Handle_ValidRequest_ResultMessageIndicatesStep1Complete()
    {
        SetupEmailAndBrandExist(false, false);
        _retailerRepoMock.Setup(x => x.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount a, CancellationToken _) => a);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.Message.Should().Contain("Step 1");
    }
}