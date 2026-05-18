using Application.Features.Settings.Commands.DeleteAccount;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Settings.Commands;

public sealed class DeleteAccountCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly Mock<ILogger<DeleteAccountCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _repoMock = new();
    private readonly DeleteAccountCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();

    public DeleteAccountCommandHandlerTests()
    {
        _currentUserServiceMock.Setup(x => x.RetailerId).Returns(RetailerId);

        _uowMock.Setup(x => x.Repository<RetailerAccount>()).Returns(_repoMock.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _repoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _publisherMock.Setup(x => x.Publish(
                It.IsAny<INotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new DeleteAccountCommandHandler(
            _uowMock.Object,
            _currentUserServiceMock.Object,
            _publisherMock.Object,
            _loggerMock.Object);
    }

    private RetailerAccount CreateActiveRetailer()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Password1!", workFactor: 4);
        var account = RetailerAccount.Create("Alice Smith", "alice@example.com", hash, "AliceBrand");
        account.CompleteRegistration("Fashion", false, null);
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

        var command = new DeleteAccountCommand();

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidRequest_SetsStatusToPendingDeletion()
    {
        var account = CreateActiveRetailer();
        SetupRetailerFound(account);

        var command = new DeleteAccountCommand();
        await _sut.Handle(command, CancellationToken.None);

        account.AccountStatus.Should().Be(RetailerAccount.Status.PendingDeletion);
    }

    [Fact]
    public async Task Handle_ValidRequest_RaisesAccountDeletionRequestedEvent()
    {
        SetupRetailerFound(CreateActiveRetailer());

        var command = new DeleteAccountCommand();
        await _sut.Handle(command, CancellationToken.None);

        _publisherMock.Verify(x => x.Publish(
            It.Is<AccountDeletionRequestedEvent>(e =>
                e.RetailerId == RetailerId &&
                !string.IsNullOrEmpty(e.RetailerEmail)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsChanges()
    {
        var account = CreateActiveRetailer();
        SetupRetailerFound(account);

        var command = new DeleteAccountCommand();
        await _sut.Handle(command, CancellationToken.None);

        _repoMock.Verify(x => x.UpdateAsync(account, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}