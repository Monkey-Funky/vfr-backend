using Application.Features.Settings.Commands.ChangePassword;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Settings.Commands;

public sealed class ChangePasswordCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ILogger<ChangePasswordCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _repoMock = new();
    private readonly ChangePasswordCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private const string CurrentPassword = "CurrentPassword1!";
    private const string NewPassword = "NewPassword99!";
    private static readonly string CurrentPasswordHash =
        BCrypt.Net.BCrypt.HashPassword(CurrentPassword, workFactor: 4);

    public ChangePasswordCommandHandlerTests()
    {
        _currentUserServiceMock.Setup(x => x.RetailerId).Returns(RetailerId);

        _uowMock.Setup(x => x.Repository<RetailerAccount>()).Returns(_repoMock.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _repoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new ChangePasswordCommandHandler(
            _uowMock.Object,
            _currentUserServiceMock.Object,
            _loggerMock.Object);
    }

    private RetailerAccount CreateActiveRetailer(string passwordHash)
    {
        var account = RetailerAccount.Create("Jane Doe", "jane@example.com", passwordHash, "JaneBrand");
        account.CompleteRegistration("Electronics", false, null);
        return account;
    }

    private void SetupRetailerFound(RetailerAccount? account)
    {
        _repoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
    }

    [Fact]
    public async Task Handle_RetailerNotFound_ThrowsNotFoundException()
    {
        SetupRetailerFound(null);

        var command = new ChangePasswordCommand(CurrentPassword, NewPassword, NewPassword);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WrongCurrentPassword_ThrowsBusinessRuleException()
    {
        SetupRetailerFound(CreateActiveRetailer(CurrentPasswordHash));

        var command = new ChangePasswordCommand("WrongPassword!", NewPassword, NewPassword);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.Handle(command, CancellationToken.None));

        ex.Code.Should().Be("INCORRECT_PASSWORD");
    }

    [Fact]
    public async Task Handle_ValidCurrentPassword_UpdatesPasswordHash()
    {
        var account = CreateActiveRetailer(CurrentPasswordHash);
        SetupRetailerFound(account);

        var command = new ChangePasswordCommand(CurrentPassword, NewPassword, NewPassword);
        await _sut.Handle(command, CancellationToken.None);

        account.PasswordHash.Should().NotBe(CurrentPasswordHash);
        BCrypt.Net.BCrypt.Verify(NewPassword, account.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NewPasswordIsBCryptHashed()
    {
        var account = CreateActiveRetailer(CurrentPasswordHash);
        SetupRetailerFound(account);

        var command = new ChangePasswordCommand(CurrentPassword, NewPassword, NewPassword);
        await _sut.Handle(command, CancellationToken.None);

        account.PasswordHash.Should().NotBe(NewPassword);
        account.PasswordHash.Should().StartWith("$2");
        BCrypt.Net.BCrypt.Verify(NewPassword, account.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsChanges()
    {
        var account = CreateActiveRetailer(CurrentPasswordHash);
        SetupRetailerFound(account);

        var command = new ChangePasswordCommand(CurrentPassword, NewPassword, NewPassword);
        await _sut.Handle(command, CancellationToken.None);

        _repoMock.Verify(x => x.UpdateAsync(account, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}