using Application.Features.Categories.Commands.CreateSubCategory;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Categories;

public sealed class CreateSubCategoryCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<Category>> _categoryRepoMock = new();
    private readonly Mock<IRepository<SubCategory>> _subCategoryRepoMock = new();
    private readonly CreateSubCategoryCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid ParentCategoryId = Guid.NewGuid();

    public CreateSubCategoryCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<Category>()).Returns(_categoryRepoMock.Object);
        _uowMock.Setup(x => x.Repository<SubCategory>()).Returns(_subCategoryRepoMock.Object);
        _currentUserMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        var parentCategory = BuildParentCategory();

        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentCategory);

        _subCategoryRepoMock
            .Setup(x => x.AnyAsync(It.IsAny<Expression<Func<SubCategory, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _subCategoryRepoMock
            .Setup(x => x.AddAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubCategory sc, CancellationToken _) => sc);

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _cacheMock
            .Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new CreateSubCategoryCommandHandler(
            _uowMock.Object,
            _currentUserMock.Object,
            _cacheMock.Object);
    }

    private static Category BuildParentCategory()
    {
        var category = Category.Create(RetailerId, "Electronics", null, "https://cdn.example.com/cover.jpg", "Active");
        typeof(Category).BaseType!.GetProperty("Id")!.SetValue(category, ParentCategoryId);
        return category;
    }

    private static CreateSubCategoryCommand BuildCommand(string name = "Smartphones") =>
        new(ParentCategoryId: ParentCategoryId, Name: name, Status: "Active");

    [Fact]
    public async Task Handle_MissingRetailerId_ThrowsUnauthorizedException()
    {
        _currentUserMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(BuildCommand(), default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ParentCategoryNotFound_ThrowsNotFoundException()
    {
        _categoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var act = () => _sut.Handle(BuildCommand(), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ParentIsAlreadySubCategory_ThrowsBusinessRuleException()
    {
        _subCategoryRepoMock
            .SetupSequence(x => x.AnyAsync(It.IsAny<Expression<Func<SubCategory, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        var act = () => _sut.Handle(BuildCommand(), default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("SUBCATEGORY_DEPTH_EXCEEDED");
    }

    [Fact]
    public async Task Handle_DuplicateNameInParentCategory_ThrowsConflictException()
    {
        _subCategoryRepoMock
            .SetupSequence(x => x.AnyAsync(It.IsAny<Expression<Func<SubCategory, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        var act = () => _sut.Handle(BuildCommand("Duplicate Name"), default);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsSubCategoryAndSavesChanges()
    {
        await _sut.Handle(BuildCommand(), default);

        _subCategoryRepoMock.Verify(
            x => x.AddAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()),
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
    public async Task Handle_ValidCommand_CreatesSubCategoryWithCorrectParentAndRetailer()
    {
        SubCategory? capturedSubCategory = null;

        _subCategoryRepoMock
            .Setup(x => x.AddAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()))
            .Callback<SubCategory, CancellationToken>((sc, _) => capturedSubCategory = sc)
            .ReturnsAsync((SubCategory sc, CancellationToken _) => sc);

        await _sut.Handle(BuildCommand("Smartphones"), default);

        capturedSubCategory.Should().NotBeNull();
        capturedSubCategory!.CategoryId.Should().Be(ParentCategoryId);
        capturedSubCategory.RetailerId.Should().Be(RetailerId);
        capturedSubCategory.Name.Should().Be("Smartphones");
    }

    [Fact]
    public async Task Handle_DbUniqueConstraintViolation_ThrowsConflictException()
    {
        var innerException = new Exception("23505: duplicate key value violates unique constraint");
        var dbException = new DbUpdateException("DB error", innerException);

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(dbException);

        var act = () => _sut.Handle(BuildCommand(), default);

        await act.Should().ThrowAsync<ConflictException>();
    }
}