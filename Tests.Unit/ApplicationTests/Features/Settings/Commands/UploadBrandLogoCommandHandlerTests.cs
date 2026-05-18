using Application.Features.Settings.Commands.UploadBrandLogo;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Tests.Unit.ApplicationTests.Features.Settings.Commands;

public sealed class UploadBrandLogoCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IFileStorageService> _fileStorageServiceMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ILogger<UploadBrandLogoCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock = new();
    private readonly UploadBrandLogoCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private const string NewLogoUrl = "https://cdn.example.com/brand-logos/new-logo.png";
    private const string OldLogoUrl = "https://cdn.example.com/brand-logos/old-logo.png";

    public UploadBrandLogoCommandHandlerTests()
    {
        _sut = new UploadBrandLogoCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _fileStorageServiceMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);

        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _unitOfWorkMock.Setup(x => x.Repository<RetailerAccount>())
            .Returns(_retailerRepoMock.Object);

        _fileStorageServiceMock
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewLogoUrl);

        _fileStorageServiceMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _retailerRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _cacheServiceMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static UploadBrandLogoCommand ValidCommand() =>
        new(FileContent: new byte[] { 0xFF, 0xD8, 0xFF }, FileName: "logo.png");

    private RetailerAccount CreateRetailer(string? existingLogoUrl = null)
    {
        var account = RetailerAccount.Create("Test Retailer", "test@example.com", "hash", "TestBrand");
        account.CompleteRegistration("Fashion", false, existingLogoUrl);
        return account;
    }

    private void SetupRetailerRepo(RetailerAccount? account) =>
        _retailerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

    [Fact]
    public async Task Handle_RetailerNotFound_ThrowsNotFoundException()
    {
        SetupRetailerRepo(null);

        var act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidFile_UploadsToStorage()
    {
        SetupRetailerRepo(CreateRetailer());

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _fileStorageServiceMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), "logo.png", "brand-logos", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidFile_UpdatesBrandLogoUrl()
    {
        var account = CreateRetailer();
        SetupRetailerRepo(account);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        account.BrandLogoUrl.Should().Be(NewLogoUrl);
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(NewLogoUrl);
    }

    [Fact]
    public async Task Handle_ValidFile_PersistsChanges()
    {
        var account = CreateRetailer();
        SetupRetailerRepo(account);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _retailerRepoMock.Verify(x => x.UpdateAsync(account, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRetailerHasExistingLogo_DeletesOldLogoBeforeUpload()
    {
        SetupRetailerRepo(CreateRetailer(existingLogoUrl: OldLogoUrl));

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _fileStorageServiceMock.Verify(
            x => x.DeleteAsync(OldLogoUrl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRetailerHasNoExistingLogo_SkipsDeleteStep()
    {
        SetupRetailerRepo(CreateRetailer(existingLogoUrl: null));

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _fileStorageServiceMock.Verify(
            x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDeleteOldLogoFails_StillUploadsNewLogoSuccessfully()
    {
        SetupRetailerRepo(CreateRetailer(existingLogoUrl: OldLogoUrl));
        _fileStorageServiceMock
            .Setup(x => x.DeleteAsync(OldLogoUrl, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable"));

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _fileStorageServiceMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidFile_InvalidatesRetailerProfileCache()
    {
        SetupRetailerRepo(CreateRetailer());

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _cacheServiceMock.Verify(
            x => x.RemoveAsync(
                It.Is<string>(k => k.Contains(RetailerId.ToString("N"))),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ValidFile_ReturnsSuccessResultWithNewUrl()
    {
        SetupRetailerRepo(CreateRetailer());

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(NewLogoUrl);
        result.Message.Should().NotBeNullOrWhiteSpace();
    }
}