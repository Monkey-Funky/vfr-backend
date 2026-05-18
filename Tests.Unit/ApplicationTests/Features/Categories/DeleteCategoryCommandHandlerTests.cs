using Application.Features.Categories.Commands.DeleteCategory;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Moq.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Categories;

public sealed class DeleteCategoryCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<Category>> _categoryRepoMock = new();
    private readonly DeleteCategoryCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private const string CoverImageUrl = "https://cdn.example.com/category-covers/cover.jpg";

    public DeleteCategoryCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<Category>()).Returns(_categoryRepoMock.Object);
        _currentUserMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _contextMock.Setup(x => x.Offers).ReturnsDbSet(new List<Domain.Entities.Retailer.Offer>());
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product>());
        _contextMock.Setup(x => x.SubCategories).ReturnsDbSet(new List<SubCategory>());

        _uowMock
            .Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _categoryRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _fileStorageMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cacheMock
            .Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new DeleteCategoryCommandHandler(
            _uowMock.Object,
            _contextMock.Object,
            _currentUserMock.Object,
            _fileStorageMock.Object,
            _cacheMock.Object);
    }

    private Category BuildCategory()
    {
        var category = Category.Create(RetailerId, "Electronics", "Description", CoverImageUrl, "Active");
        typeof(Category).BaseType!.GetProperty("Id")!.SetValue(category, CategoryId);
        return category;
    }

    [Fact]
    public async Task Handle_MissingRetailerId_ThrowsUnauthorizedException()
    {
        _currentUserMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new DeleteCategoryCommand(CategoryId), default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_CategoryNotFound_ThrowsNotFoundException()
    {
        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var act = () => _sut.Handle(new DeleteCategoryCommand(CategoryId), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CategoryNotFound_DoesNotStartTransaction()
    {
        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var act = () => _sut.Handle(new DeleteCategoryCommand(CategoryId), default);

        await act.Should().ThrowAsync<NotFoundException>();

        _uowMock.Verify(
            x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidCommand_ExecutesTransaction()
    {
        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCategory());

        await _sut.Handle(new DeleteCategoryCommand(CategoryId), default);

        _uowMock.Verify(
            x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_DeletesCoverImageFromStorageAfterTransaction()
    {
        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCategory());

        await _sut.Handle(new DeleteCategoryCommand(CategoryId), default);

        _fileStorageMock.Verify(
            x => x.DeleteAsync(CoverImageUrl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_InvalidatesCachePrefixForRetailer()
    {
        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCategory());

        await _sut.Handle(new DeleteCategoryCommand(CategoryId), default);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync($"categories:{RetailerId}:", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessResult()
    {
        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCategory());

        var result = await _sut.Handle(new DeleteCategoryCommand(CategoryId), default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidCommand_CacheInvalidatedAfterFileStorageDelete()
    {
        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCategory());

        var callOrder = new List<string>();

        _fileStorageMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("storage"))
            .Returns(Task.CompletedTask);

        _cacheMock
            .Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("cache"))
            .Returns(Task.CompletedTask);

        await _sut.Handle(new DeleteCategoryCommand(CategoryId), default);

        callOrder.Should().ContainInOrder("storage", "cache");
    }
}