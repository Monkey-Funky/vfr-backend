using Application.Features.Categories.Commands.ToggleCategoryStatus;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Categories;

public sealed class ToggleCategoryStatusCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<Category>> _categoryRepoMock = new();
    private readonly ToggleCategoryStatusCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public ToggleCategoryStatusCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<Category>()).Returns(_categoryRepoMock.Object);
        _currentUserMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _categoryRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _cacheMock
            .Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new ToggleCategoryStatusCommandHandler(
            _uowMock.Object,
            _currentUserMock.Object,
            _cacheMock.Object);
    }

    private Category BuildCategory(string status = "Active")
    {
        var category = Category.Create(RetailerId, "Electronics", null, "https://cdn.example.com/cover.jpg", status);
        typeof(Category).BaseType!.GetProperty("Id")!.SetValue(category, CategoryId);
        return category;
    }

    [Fact]
    public async Task Handle_MissingRetailerId_ThrowsUnauthorizedException()
    {
        _currentUserMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new ToggleCategoryStatusCommand(CategoryId), default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_CategoryNotFound_ThrowsNotFoundException()
    {
        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var act = () => _sut.Handle(new ToggleCategoryStatusCommand(CategoryId), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ActiveCategory_TogglesStatusToInactive()
    {
        var category = BuildCategory("Active");

        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var result = await _sut.Handle(new ToggleCategoryStatusCommand(CategoryId), default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.NewStatus.Should().Be("Inactive");
        category.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task Handle_InactiveCategory_TogglesStatusToActive()
    {
        var category = BuildCategory("Inactive");

        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var result = await _sut.Handle(new ToggleCategoryStatusCommand(CategoryId), default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.NewStatus.Should().Be("Active");
        category.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesChanges()
    {
        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCategory());

        await _sut.Handle(new ToggleCategoryStatusCommand(CategoryId), default);

        _categoryRepoMock.Verify(
            x => x.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_InvalidatesCachePrefixForRetailer()
    {
        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCategory());

        await _sut.Handle(new ToggleCategoryStatusCommand(CategoryId), default);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync($"categories:{RetailerId}:", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsNewStatusInResult()
    {
        var category = BuildCategory("Active");

        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var result = await _sut.Handle(new ToggleCategoryStatusCommand(CategoryId), default);

        result.Data.Should().NotBeNull();
        result.Data!.NewStatus.Should().Be("Inactive");
    }

    [Fact]
    public async Task Handle_ToggleCalledTwice_RestoresOriginalStatus()
    {
        var category = BuildCategory("Active");

        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var firstResult = await _sut.Handle(new ToggleCategoryStatusCommand(CategoryId), default);
        var secondResult = await _sut.Handle(new ToggleCategoryStatusCommand(CategoryId), default);

        firstResult.Data!.NewStatus.Should().Be("Inactive");
        secondResult.Data!.NewStatus.Should().Be("Active");
    }
}