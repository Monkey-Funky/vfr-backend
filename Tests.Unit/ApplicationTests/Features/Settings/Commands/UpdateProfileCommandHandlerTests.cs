using Application.Features.Settings.Commands.UpdateProfile;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Settings.Commands;

public sealed class UpdateProfileCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ILogger<UpdateProfileCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _repoMock = new();
    private readonly UpdateProfileCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();

    public UpdateProfileCommandHandlerTests()
    {
        _currentUserServiceMock.Setup(x => x.RetailerId).Returns(RetailerId);

        _uowMock.Setup(x => x.Repository<RetailerAccount>()).Returns(_repoMock.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _repoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repoMock.Setup(x => x.AnyAsync(
                It.IsAny<Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new UpdateProfileCommandHandler(
            _uowMock.Object,
            _currentUserServiceMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    private RetailerAccount CreateActiveRetailer()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Password1!", workFactor: 4);
        var account = RetailerAccount.Create("John Doe", "john@example.com", hash, "OriginalBrand");
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

        var command = new UpdateProfileCommand("New Name", null, null, null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidData_UpdatesFullNameAndBio()
    {
        var account = CreateActiveRetailer();
        SetupRetailerFound(account);

        var command = new UpdateProfileCommand("Updated Name", "+1234567890", "UpdatedBrand", "Retail");
        await _sut.Handle(command, CancellationToken.None);

        account.FullName.Should().Be("Updated Name");
        account.BrandName.Should().Be("UpdatedBrand");
        account.BusinessType.Should().Be("Retail");
    }

    [Fact]
    public async Task Handle_ValidData_PersistsChanges()
    {
        var account = CreateActiveRetailer();
        SetupRetailerFound(account);

        var command = new UpdateProfileCommand("Updated Name", null, null, null);
        await _sut.Handle(command, CancellationToken.None);

        _repoMock.Verify(x => x.UpdateAsync(account, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidData_ReturnsUpdatedProfile()
    {
        SetupRetailerFound(CreateActiveRetailer());

        var command = new UpdateProfileCommand("Updated Name", null, null, null);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        result.Message.Should().NotBeNullOrEmpty();
    }
}