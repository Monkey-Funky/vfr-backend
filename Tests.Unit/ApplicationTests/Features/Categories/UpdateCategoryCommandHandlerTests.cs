using Application.Features.Categories.Commands.UpdateCategory;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Categories;

public sealed class UpdateCategoryCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<Category>> _categoryRepoMock = new();
    private readonly UpdateCategoryCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private const string ExistingCoverUrl = "https://cdn.example.com/old-cover.jpg";
    private const string NewCoverUrl = "https://cdn.example.com/new-cover.jpg";

    public UpdateCategoryCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<Category>()).Returns(_categoryRepoMock.Object);
        _currentUserMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        var existingCategory = BuildCategory();

        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCategory);

        _categoryRepoMock
            .Setup(x => x.AnyAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _categoryRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _fileStorageMock
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), "category-covers", It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewCoverUrl);

        _fileStorageMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cacheMock
            .Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new UpdateCategoryCommandHandler(
            _uowMock.Object,
            _currentUserMock.Object,
            _fileStorageMock.Object,
            _cacheMock.Object);
    }

    private static Category BuildCategory(string name = "Old Name", string status = "Active")
    {
        var category = Category.Create(RetailerId, name, "Old Description", ExistingCoverUrl, status);
        typeof(Category).BaseType!.GetProperty("Id")!.SetValue(category, CategoryId);
        return category;
    }

    private static UpdateCategoryCommand BuildCommand(
        string? newName = "New Name",
        string? newDescription = null,
        bool shouldUpdateDescription = false,
        Stream? newImageStream = null,
        string? newImageFileName = null,
        string? status = null) =>
        new(
            CategoryId: CategoryId,
            NewName: newName,
            NewDescription: newDescription,
            ShouldUpdateDescription: shouldUpdateDescription,
            NewCoverImageStream: newImageStream,
            NewCoverImageFileName: newImageFileName,
            NewCoverImageContentType: newImageStream is null ? null : "image/jpeg",
            Status: status);

    [Fact]
    public async Task Handle_MissingRetailerId_ThrowsUnauthorizedException()
    {
        _currentUserMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(BuildCommand(), default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_CategoryNotFound_ThrowsNotFoundException()
    {
        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var act = () => _sut.Handle(BuildCommand(), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DuplicateNameForDifferentCategory_ThrowsConflictException()
    {
        _categoryRepoMock
            .Setup(x => x.AnyAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _sut.Handle(BuildCommand(newName: "Conflicting Name"), default);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_WithNullName_SkipsNameUniquenessCheck()
    {
        var result = await _sut.Handle(BuildCommand(newName: null), default);

        result.IsSuccess.Should().BeTrue();

        _categoryRepoMock.Verify(
            x => x.AnyAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidCommand_CallsUpdateAndSave()
    {
        await _sut.Handle(BuildCommand(), default);

        _categoryRepoMock.Verify(
            x => x.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessResult()
    {
        var result = await _sut.Handle(BuildCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithNewCoverImage_UploadsNewImageBeforeSave()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = BuildCommand(newImageStream: stream, newImageFileName: "new.jpg");

        await _sut.Handle(command, default);

        _fileStorageMock.Verify(
            x => x.UploadAsync(stream, "new.jpg", "category-covers", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNewCoverImage_DeletesOldImageAfterSuccessfulSave()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = BuildCommand(newImageStream: stream, newImageFileName: "new.jpg");

        await _sut.Handle(command, default);

        _fileStorageMock.Verify(
            x => x.DeleteAsync(ExistingCoverUrl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutNewCoverImage_DoesNotUploadOrDeleteImages()
    {
        await _sut.Handle(BuildCommand(newImageStream: null), default);

        _fileStorageMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _fileStorageMock.Verify(
            x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
    public async Task Handle_DbUniqueConstraintViolation_WithNewImage_DeletesNewImageAndThrowsConflictException()
    {
        var innerException = new Exception("23505: duplicate key value violates unique constraint");
        var dbException = new DbUpdateException("DB error", innerException);

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(dbException);

        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = BuildCommand(newImageStream: stream, newImageFileName: "new.jpg");

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<ConflictException>();

        _fileStorageMock.Verify(
            x => x.DeleteAsync(NewCoverUrl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DbSaveThrowsGenericException_WithNewImage_DeletesNewImageAndRethrows()
    {
        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = BuildCommand(newImageStream: stream, newImageFileName: "new.jpg");

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<InvalidOperationException>();

        _fileStorageMock.Verify(
            x => x.DeleteAsync(NewCoverUrl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldUpdateDescription_WithNullValue_ClearsDescription()
    {
        Category? updatedCategory = null;
        _categoryRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Callback<Category, CancellationToken>((c, _) => updatedCategory = c)
            .Returns(Task.CompletedTask);

        var command = BuildCommand(newName: null, newDescription: null, shouldUpdateDescription: true);
        await _sut.Handle(command, default);

        updatedCategory.Should().NotBeNull();
        updatedCategory!.Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNewStatus_UpdatesStatus()
    {
        Category? updatedCategory = null;
        _categoryRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Callback<Category, CancellationToken>((c, _) => updatedCategory = c)
            .Returns(Task.CompletedTask);

        var command = BuildCommand(newName: null, status: "Inactive");
        await _sut.Handle(command, default);

        updatedCategory.Should().NotBeNull();
        updatedCategory!.Status.Should().Be("Inactive");
    }
}