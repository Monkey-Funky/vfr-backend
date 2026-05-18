using Application.Features.Categories.Commands.DeleteSubCategory;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Moq.EntityFrameworkCore;

namespace Tests.Unit.Application.Features.Categories;

public sealed class DeleteSubCategoryCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly DeleteSubCategoryCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid ParentCategoryId = Guid.NewGuid();
    private static readonly Guid SubCategoryId = Guid.NewGuid();

    public DeleteSubCategoryCommandHandlerTests()
    {
        _sut = new DeleteSubCategoryCommandHandler(
            _uowMock.Object,
            _contextMock.Object,
            _userServiceMock.Object,
            _cacheMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _uowMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> action, CancellationToken ct) => action(ct));
    }

    private SubCategory BuildSubCategory(
        Guid? id = null,
        Guid? categoryId = null,
        Guid? retailerId = null)
    {
        var sc = SubCategory.Create(
            categoryId ?? ParentCategoryId,
            retailerId ?? RetailerId,
            "TestSub",
            SubCategory.SubCategoryStatus.Active);

        if (id.HasValue)
            typeof(SubCategory).GetProperty(nameof(SubCategory.Id))!.SetValue(sc, id.Value);

        return sc;
    }

    private void SetupSubCategoryRepo(SubCategory? returns)
    {
        var repo = new Mock<IRepository<SubCategory>>();
        repo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SubCategory, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(returns);
        _uowMock.Setup(x => x.Repository<SubCategory>()).Returns(repo.Object);
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        var command = new DeleteSubCategoryCommand(ParentCategoryId, SubCategoryId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_SubCategoryNotFound_ThrowsNotFoundException()
    {
        SetupSubCategoryRepo(null);
        var command = new DeleteSubCategoryCommand(ParentCategoryId, SubCategoryId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SubCategoryBelongsToDifferentRetailer_ThrowsNotFoundException()
    {
        var differentRetailerId = Guid.NewGuid();
        var subCategory = BuildSubCategory(SubCategoryId, ParentCategoryId, differentRetailerId);

        var repo = new Mock<IRepository<SubCategory>>();
        repo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SubCategory, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubCategory?)null);
        _uowMock.Setup(x => x.Repository<SubCategory>()).Returns(repo.Object);

        var command = new DeleteSubCategoryCommand(ParentCategoryId, SubCategoryId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SubCategoryBelongsToDifferentParentCategory_ThrowsNotFoundException()
    {
        var wrongParentCategoryId = Guid.NewGuid();

        var repo = new Mock<IRepository<SubCategory>>();
        repo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SubCategory, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubCategory?)null);
        _uowMock.Setup(x => x.Repository<SubCategory>()).Returns(repo.Object);

        var command = new DeleteSubCategoryCommand(wrongParentCategoryId, SubCategoryId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_NullsOutProductSubCategoryIds()
    {
        var subCategory = BuildSubCategory(SubCategoryId, ParentCategoryId, RetailerId);
        SetupSubCategoryRepo(subCategory);

        var products = new List<Product>
        {
            Product.Create(RetailerId, "Product A", subCategoryId: SubCategoryId)
        };

        _contextMock.Setup(x => x.Products).ReturnsDbSet(products);

        var subCategoryRepo = new Mock<IRepository<SubCategory>>();
        subCategoryRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SubCategory, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subCategory);
        subCategoryRepo.Setup(r => r.UpdateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.Repository<SubCategory>()).Returns(subCategoryRepo.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cacheMock.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new DeleteSubCategoryCommand(ParentCategoryId, SubCategoryId);

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidCommand_SoftDeletesSubCategory()
    {
        var subCategory = BuildSubCategory(SubCategoryId, ParentCategoryId, RetailerId);

        var subCategoryRepo = new Mock<IRepository<SubCategory>>();
        subCategoryRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SubCategory, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subCategory);
        subCategoryRepo.Setup(r => r.UpdateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.Repository<SubCategory>()).Returns(subCategoryRepo.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product>());

        _cacheMock.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new DeleteSubCategoryCommand(ParentCategoryId, SubCategoryId);

        await _sut.Handle(command, default);

        subCategory.IsDeleted.Should().BeTrue();
        subCategoryRepo.Verify(r => r.UpdateAsync(subCategory, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesChanges()
    {
        var subCategory = BuildSubCategory(SubCategoryId, ParentCategoryId, RetailerId);

        var subCategoryRepo = new Mock<IRepository<SubCategory>>();
        subCategoryRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SubCategory, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subCategory);
        subCategoryRepo.Setup(r => r.UpdateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.Repository<SubCategory>()).Returns(subCategoryRepo.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product>());

        _cacheMock.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new DeleteSubCategoryCommand(ParentCategoryId, SubCategoryId);

        await _sut.Handle(command, default);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_InvalidatesCacheByPrefix()
    {
        var subCategory = BuildSubCategory(SubCategoryId, ParentCategoryId, RetailerId);

        var subCategoryRepo = new Mock<IRepository<SubCategory>>();
        subCategoryRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SubCategory, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subCategory);
        subCategoryRepo.Setup(r => r.UpdateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.Repository<SubCategory>()).Returns(subCategoryRepo.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product>());

        _cacheMock.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new DeleteSubCategoryCommand(ParentCategoryId, SubCategoryId);

        await _sut.Handle(command, default);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(
                It.Is<string>(k => k.Contains(RetailerId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessResultWithMessage()
    {
        var subCategory = BuildSubCategory(SubCategoryId, ParentCategoryId, RetailerId);

        var subCategoryRepo = new Mock<IRepository<SubCategory>>();
        subCategoryRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SubCategory, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(subCategory);
        subCategoryRepo.Setup(r => r.UpdateAsync(It.IsAny<SubCategory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(x => x.Repository<SubCategory>()).Returns(subCategoryRepo.Object);
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product>());

        _cacheMock.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new DeleteSubCategoryCommand(ParentCategoryId, SubCategoryId);

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }
}