using Application.Features.Settings.Commands.UploadAvatar;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Settings.Commands;

public sealed class UploadAvatarCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ILogger<UploadAvatarCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _repoMock = new();
    private readonly UploadAvatarCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private const string NewAvatarUrl = "https://cdn.example.com/avatars/new-avatar.png";

    public UploadAvatarCommandHandlerTests()
    {
        _currentUserServiceMock.Setup(x => x.RetailerId).Returns(RetailerId);

        _uowMock.Setup(x => x.Repository<RetailerAccount>()).Returns(_repoMock.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _repoMock.Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fileStorageMock.Setup(x => x.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewAvatarUrl);

        _fileStorageMock.Setup(x => x.DeleteAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cacheServiceMock.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new UploadAvatarCommandHandler(
            _uowMock.Object,
            _currentUserServiceMock.Object,
            _fileStorageMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    private RetailerAccount CreateActiveRetailer(string? existingAvatarUrl = null)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Password1!", workFactor: 4);
        var account = RetailerAccount.Create("Bob Doe", "bob@example.com", hash, "BobBrand");
        account.CompleteRegistration("Sports", false, null);
        if (existingAvatarUrl is not null)
            account.SetAvatarUrl(existingAvatarUrl);
        return account;
    }

    private void SetupRetailerFound(RetailerAccount? account)
    {
        _repoMock.Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
    }

    private static UploadAvatarCommand BuildCommand() =>
        new(FileContent: new byte[] { 1, 2, 3, 4 }, FileName: "avatar.png");

    [Fact]
    public async Task Handle_RetailerNotFound_ThrowsNotFoundException()
    {
        SetupRetailerFound(null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.Handle(BuildCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidFile_UploadsToStorage()
    {
        SetupRetailerFound(CreateActiveRetailer());

        await _sut.Handle(BuildCommand(), CancellationToken.None);

        _fileStorageMock.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(),
            "avatar.png",
            "avatars",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidFile_UpdatesAvatarUrl()
    {
        var account = CreateActiveRetailer();
        SetupRetailerFound(account);

        await _sut.Handle(BuildCommand(), CancellationToken.None);

        account.AvatarUrl.Should().Be(NewAvatarUrl);
    }

    [Fact]
    public async Task Handle_ValidFile_PersistsChanges()
    {
        var account = CreateActiveRetailer();
        SetupRetailerFound(account);

        await _sut.Handle(BuildCommand(), CancellationToken.None);

        _repoMock.Verify(x => x.UpdateAsync(account, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidFile_ReturnsNewAvatarUrl()
    {
        SetupRetailerFound(CreateActiveRetailer());

        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(NewAvatarUrl);
    }
}