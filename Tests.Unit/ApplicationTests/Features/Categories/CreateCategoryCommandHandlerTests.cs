using Application.Features.Categories.Commands.CreateCategory;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Categories;

public sealed class CreateCategoryCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<Category>> _categoryRepoMock = new();
    private readonly CreateCategoryCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private const string CoverImageUrl = "https://cdn.example.com/category-covers/abc.jpg";

    public CreateCategoryCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<Category>()).Returns(_categoryRepoMock.Object);

        _currentUserMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _fileStorageMock
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), "category-covers", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CoverImageUrl);

        _fileStorageMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _categoryRepoMock
            .Setup(x => x.AnyAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _categoryRepoMock
            .Setup(x => x.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category c, CancellationToken _) => c);

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _cacheMock
            .Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new CreateCategoryCommandHandler(
            _uowMock.Object,
            _currentUserMock.Object,
            _fileStorageMock.Object,
            _cacheMock.Object);
    }

    private static CreateCategoryCommand BuildCommand(string name = "Electronics") =>
        new(
            Name: name,
            Description: "Test description",
            CoverImageStream: new MemoryStream(new byte[] { 1, 2, 3 }),
            CoverImageFileName: "cover.jpg",
            CoverImageContentType: "image/jpeg",
            Status: "Active");

    [Fact]
    public async Task Handle_MissingRetailerId_ThrowsUnauthorizedException()
    {
        _currentUserMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(BuildCommand(), default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_DuplicateCategoryName_ThrowsConflictException()
    {
        _categoryRepoMock
            .Setup(x => x.AnyAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _sut.Handle(BuildCommand("Duplicate Name"), default);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_DuplicateCategoryName_DoesNotUploadCoverImage()
    {
        _categoryRepoMock
            .Setup(x => x.AnyAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _sut.Handle(BuildCommand(), default);

        await act.Should().ThrowAsync<ConflictException>();

        _fileStorageMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidCommand_UploadsCoverImageToCorrectFolder()
    {
        await _sut.Handle(BuildCommand(), default);

        _fileStorageMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), "cover.jpg", "category-covers", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsCategoryViaRepository()
    {
        await _sut.Handle(BuildCommand(), default);

        _categoryRepoMock.Verify(
            x => x.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessResultWithNonEmptyGuid()
    {
        var result = await _sut.Handle(BuildCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ValidCommand_InvalidatesCachePrefixForRetailer()
    {
        await _sut.Handle(BuildCommand(), default);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync($"categories:{RetailerId}:", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DbUniqueConstraintViolation_DeletesUploadedImageAndThrowsConflictException()
    {
        var innerException = new Exception("23505: duplicate key violates unique constraint");
        var dbException = new DbUpdateException("DB error", innerException);

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(dbException);

        var act = () => _sut.Handle(BuildCommand(), default);

        await act.Should().ThrowAsync<ConflictException>();

        _fileStorageMock.Verify(
            x => x.DeleteAsync(CoverImageUrl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DbSaveThrowsGenericException_DeletesUploadedImageAndRethrows()
    {
        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected DB error"));

        var act = () => _sut.Handle(BuildCommand(), default);

        await act.Should().ThrowAsync<InvalidOperationException>();

        _fileStorageMock.Verify(
            x => x.DeleteAsync(CoverImageUrl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_DoesNotDeleteImageOnSuccess()
    {
        await _sut.Handle(BuildCommand(), default);

        _fileStorageMock.Verify(
            x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}