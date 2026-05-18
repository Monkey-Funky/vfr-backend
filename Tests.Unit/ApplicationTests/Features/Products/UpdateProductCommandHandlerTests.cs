using Application.Features.Products.Commands.UpdateProduct;
using Application.Features.Products.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Moq.EntityFrameworkCore;
using Shared.Constants;

namespace Tests.Unit.Application.Features.Products;

public sealed class UpdateProductCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly UpdateProductCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public UpdateProductCommandHandlerTests()
    {
        _sut = new UpdateProductCommandHandler(
            _uowMock.Object,
            _userServiceMock.Object,
            _contextMock.Object,
            _cacheMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private Product BuildProduct(
        Guid? id = null,
        Guid? retailerId = null,
        string name = "Original Name",
        string status = ProductStatus.Active,
        bool isDeleted = false)
    {
        var product = Product.Create(
            retailerId ?? RetailerId,
            name,
            status: status);

        var productId = id ?? ProductId;
        typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(product, productId);

        if (isDeleted)
            product.SoftDelete();

        return product;
    }

    private void SetupProductExistsInContext(bool exists, Guid? productId = null, Guid? retailerId = null)
    {
        var pid = productId ?? ProductId;
        var rid = retailerId ?? RetailerId;

        if (exists)
        {
            var product = BuildProduct(pid, rid);
            _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product> { product });
        }
        else
        {
            _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product>());
        }
    }

    private void SetupCategoryExistsInContext(bool exists, Guid? categoryId = null)
    {
        var cid = categoryId ?? Guid.NewGuid();
        if (exists)
        {
            var category = Category.Create(RetailerId, "Cat", null, "icon", Category.CategoryStatus.Active);
            typeof(Category).GetProperty(nameof(Category.Id))!.SetValue(category, cid);
            _contextMock.Setup(x => x.Categories).ReturnsDbSet(new List<Category> { category });
        }
        else
        {
            _contextMock.Setup(x => x.Categories).ReturnsDbSet(new List<Category>());
        }
    }

    private void SetupSubCategoryExistsInContext(bool exists, Guid subCategoryId, Guid categoryId)
    {
        if (exists)
        {
            var sub = SubCategory.Create(categoryId, RetailerId, "Sub", SubCategory.SubCategoryStatus.Active);
            typeof(SubCategory).GetProperty(nameof(SubCategory.Id))!.SetValue(sub, subCategoryId);
            _contextMock.Setup(x => x.SubCategories).ReturnsDbSet(new List<SubCategory> { sub });
        }
        else
        {
            _contextMock.Setup(x => x.SubCategories).ReturnsDbSet(new List<SubCategory>());
        }
    }

    private void SetupTrackedProduct(Product? product)
    {
        _uowMock.Setup(x => x.GetTrackedByIdAsync<Product>(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
    }

    private void SetupNoInventoryRecord()
    {
        _contextMock.Setup(x => x.InventoryRecords).ReturnsDbSet(new List<InventoryRecord>());
    }

    private void SetupCategoriesAndSubCategoriesEmpty()
    {
        _contextMock.Setup(x => x.Categories).ReturnsDbSet(new List<Category>());
        _contextMock.Setup(x => x.SubCategories).ReturnsDbSet(new List<SubCategory>());
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        var command = new UpdateProductCommand(
            ProductId, null, null, false, null, false, null, false, null, false, null, null);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ProductNotFound_ThrowsNotFoundException()
    {
        SetupProductExistsInContext(false);
        var command = new UpdateProductCommand(
            ProductId, null, null, false, null, false, null, false, null, false, null, null);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ProductBelongsToDifferentRetailer_ThrowsNotFoundException()
    {
        var differentRetailerId = Guid.NewGuid();
        var product = BuildProduct(ProductId, differentRetailerId);
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product> { product });

        var command = new UpdateProductCommand(
            ProductId, null, null, false, null, false, null, false, null, false, null, null);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldUpdateCategory_CategoryNotFound_ThrowsNotFoundException()
    {
        SetupProductExistsInContext(true);
        var newCategoryId = Guid.NewGuid();
        SetupCategoryExistsInContext(false, newCategoryId);

        var command = new UpdateProductCommand(
            ProductId, null, null, false, null, false, null, false,
            newCategoryId, ShouldUpdateCategory: true, null, null);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldUpdateCategory_SubCategoryNotBelongingToCategory_ThrowsNotFoundException()
    {
        SetupProductExistsInContext(true);
        var newCategoryId = Guid.NewGuid();
        var newSubCategoryId = Guid.NewGuid();
        SetupCategoryExistsInContext(true, newCategoryId);
        SetupSubCategoryExistsInContext(false, newSubCategoryId, newCategoryId);

        var command = new UpdateProductCommand(
            ProductId, null, null, false, null, false, null, false,
            newCategoryId, ShouldUpdateCategory: true, newSubCategoryId, null);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldUpdateBarcode_DuplicateBarcode_ThrowsConflictException()
    {
        const string conflictingBarcode = "BARCODE123";
        var conflictingProductId = Guid.NewGuid();

        var existingProduct = Product.Create(RetailerId, "Target Product");
        typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(existingProduct, ProductId);

        var conflictingProduct = Product.Create(RetailerId, "Other Product", barcode: conflictingBarcode);
        typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(conflictingProduct, conflictingProductId);

        _contextMock.Setup(x => x.Products)
            .ReturnsDbSet(new List<Product> { existingProduct, conflictingProduct });

        var command = new UpdateProductCommand(
            ProductId, null, null, false, null, false,
            conflictingBarcode, ShouldUpdateBarcode: true, null, false, null, null);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesName_ReturnsSuccessResult()
    {
        var product = BuildProduct(ProductId, RetailerId, "Old Name");
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product> { product });
        SetupTrackedProduct(product);
        SetupNoInventoryRecord();
        SetupCategoriesAndSubCategoriesEmpty();
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateProductCommand(
            ProductId, "New Name", null, false, null, false, null, false, null, false, null, null);

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesDescription_WhenShouldUpdateDescriptionIsTrue()
    {
        var product = BuildProduct();
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product> { product });
        SetupTrackedProduct(product);
        SetupNoInventoryRecord();
        SetupCategoriesAndSubCategoriesEmpty();
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateProductCommand(
            ProductId, null, "New Description", ShouldUpdateDescription: true,
            null, false, null, false, null, false, null, null);

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        product.Description.Should().Be("New Description");
    }

    [Fact]
    public async Task Handle_ValidCommand_ClearsDescription_WhenShouldUpdateDescriptionIsTrueAndValueIsNull()
    {
        var product = BuildProduct();
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product> { product });
        SetupTrackedProduct(product);
        SetupNoInventoryRecord();
        SetupCategoriesAndSubCategoriesEmpty();
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateProductCommand(
            ProductId, null, null, ShouldUpdateDescription: true,
            null, false, null, false, null, false, null, null);

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        product.Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesInventoryProductName_WhenNameIsChanged()
    {
        var product = BuildProduct(ProductId, RetailerId, "Old Name");
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product> { product });
        SetupTrackedProduct(product);

        var inventory = InventoryRecord.Create(RetailerId, ProductId, "Old Name", 10);
        _contextMock.Setup(x => x.InventoryRecords).ReturnsDbSet(new List<InventoryRecord> { inventory });

        SetupCategoriesAndSubCategoriesEmpty();
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateProductCommand(
            ProductId, "New Name", null, false, null, false, null, false, null, false, null, null);

        await _sut.Handle(command, default);

        inventory.ProductName.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesChanges()
    {
        var product = BuildProduct();
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product> { product });
        SetupTrackedProduct(product);
        SetupNoInventoryRecord();
        SetupCategoriesAndSubCategoriesEmpty();
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateProductCommand(
            ProductId, "Updated", null, false, null, false, null, false, null, false, null, null);

        await _sut.Handle(command, default);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_InvalidatesCacheKey()
    {
        var product = BuildProduct();
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product> { product });
        SetupTrackedProduct(product);
        SetupNoInventoryRecord();
        SetupCategoriesAndSubCategoriesEmpty();
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateProductCommand(
            ProductId, "Updated", null, false, null, false, null, false, null, false, null, null);

        await _sut.Handle(command, default);

        _cacheMock.Verify(
            x => x.RemoveAsync(
                CacheKeys.ActiveProductCount(RetailerId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsProductDetailDto()
    {
        var product = BuildProduct();
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product> { product });
        SetupTrackedProduct(product);
        SetupNoInventoryRecord();
        SetupCategoriesAndSubCategoriesEmpty();
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateProductCommand(
            ProductId, "Updated Name", null, false, null, false, null, false, null, false, null, null);

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeOfType<ProductDetailDto>();
        result.Data!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task Handle_TrackedProductNotFoundAfterOwnershipCheck_ThrowsNotFoundException()
    {
        var product = BuildProduct();
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product> { product });
        SetupTrackedProduct(null);
        SetupCategoriesAndSubCategoriesEmpty();

        var command = new UpdateProductCommand(
            ProductId, "Name", null, false, null, false, null, false, null, false, null, null);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}