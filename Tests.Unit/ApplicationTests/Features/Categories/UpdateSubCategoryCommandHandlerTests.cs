using Application.Features.Categories.Commands.UpdateSubCategory;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Categories;

public sealed class UpdateSubCategoryCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<SubCategory>> _subCategoryRepoMock = new();
    private readonly UpdateSubCategoryCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid ParentCategoryId = Guid.NewGuid();
    private static readonly Guid SubCategoryId = Guid.NewGuid();

    public UpdateSubCategoryCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<SubCategory>()).Returns(_subCategoryRepoMock.Object);
        _currentUserMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        var existingSubCategory = BuildSubCategory();

        _subCategoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<SubCategory, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSubCategory);

        _subCategoryRepoMock
            .Setup(x => x.AnyAsync(It.IsAny<Expression<Func<SubCategory, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _subCategoryRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _cacheMock
            .Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new UpdateSubCategoryCommandHandler(
            _uowMock.Object,
            _currentUserMock.Object,
            _cacheMock.Object);
    }

    private static SubCategory BuildSubCategory(string name = "Smartphones", string status = "Active")
    {
        var subCategory = SubCategory.Create(ParentCategoryId, RetailerId, name, status);
        typeof(SubCategory).BaseType!.GetProperty("Id")!.SetValue(subCategory, SubCategoryId);
        return subCategory;
    }

    private static UpdateSubCategoryCommand BuildCommand(
        string? newName = "Updated Name",
        string? status = null) =>
        new(
            ParentCategoryId: ParentCategoryId,
            SubCategoryId: SubCategoryId,
            NewName: newName,
            Status: status);

    [Fact]
    public async Task Handle_MissingRetailerId_ThrowsUnauthorizedException()
    {
        _currentUserMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(BuildCommand(), default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_SubCategoryNotFound_ThrowsNotFoundException()
    {
        _subCategoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<SubCategory, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubCategory?)null);

        var act = () => _sut.Handle(BuildCommand(), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SubCategoryBelongsToDifferentParent_ThrowsNotFoundException()
    {
        var command = new UpdateSubCategoryCommand(
            ParentCategoryId: Guid.NewGuid(),
            SubCategoryId: SubCategoryId,
            NewName: "Name",
            Status: null);

        _subCategoryRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<SubCategory, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubCategory?)null);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DuplicateNameWithinParent_ThrowsConflictException()
    {
        _subCategoryRepoMock
            .Setup(x => x.AnyAsync(It.IsAny<Expression<Func<SubCategory, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _sut.Handle(BuildCommand(newName: "Duplicate Name"), default);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_WithNullName_SkipsUniquenessCheck()
    {
        var result = await _sut.Handle(BuildCommand(newName: null), default);

        result.IsSuccess.Should().BeTrue();

        _subCategoryRepoMock.Verify(
            x => x.AnyAsync(It.IsAny<Expression<Func<SubCategory, bool>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ValidCommand_WithNewName_UpdatesSubCategoryName()
    {
        SubCategory? updatedSubCategory = null;

        _subCategoryRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()))
            .Callback<SubCategory, CancellationToken>((sc, _) => updatedSubCategory = sc)
            .Returns(Task.CompletedTask);

        await _sut.Handle(BuildCommand(newName: "New Smartphones"), default);

        updatedSubCategory.Should().NotBeNull();
        updatedSubCategory!.Name.Should().Be("New Smartphones");
    }

    [Fact]
    public async Task Handle_ValidCommand_WithNewStatus_UpdatesStatus()
    {
        SubCategory? updatedSubCategory = null;

        _subCategoryRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()))
            .Callback<SubCategory, CancellationToken>((sc, _) => updatedSubCategory = sc)
            .Returns(Task.CompletedTask);

        await _sut.Handle(BuildCommand(newName: null, status: "Inactive"), default);

        updatedSubCategory.Should().NotBeNull();
        updatedSubCategory!.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task Handle_ValidCommand_WithNullFields_DoesNotChangeExistingValues()
    {
        SubCategory? updatedSubCategory = null;

        _subCategoryRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()))
            .Callback<SubCategory, CancellationToken>((sc, _) => updatedSubCategory = sc)
            .Returns(Task.CompletedTask);

        await _sut.Handle(BuildCommand(newName: null, status: null), default);

        updatedSubCategory.Should().NotBeNull();
        updatedSubCategory!.Name.Should().Be("Smartphones");
        updatedSubCategory.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_ValidCommand_CallsUpdateAndSave()
    {
        await _sut.Handle(BuildCommand(), default);

        _subCategoryRepoMock.Verify(
            x => x.UpdateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task Handle_ValidCommand_ReturnsSuccessResult()
    {
        var result = await _sut.Handle(BuildCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }
}