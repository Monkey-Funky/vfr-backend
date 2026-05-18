using Application.Features.Products.Commands.DeleteProduct;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Moq.EntityFrameworkCore;
using Shared.Constants;

namespace Tests.Unit.Application.Features.Products;

public sealed class DeleteProductCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly DeleteProductCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public DeleteProductCommandHandlerTests()
    {
        _sut = new DeleteProductCommandHandler(
            _uowMock.Object,
            _userServiceMock.Object,
            _contextMock.Object,
            _cacheMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uowMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> action, CancellationToken ct) => action(ct));

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private Product BuildProduct(Guid? id = null, Guid? retailerId = null, bool isDeleted = false)
    {
        var product = Product.Create(retailerId ?? RetailerId, "Test Product");
        var pid = id ?? ProductId;
        typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(product, pid);
        if (isDeleted) product.SoftDelete();
        return product;
    }

    private void SetupProductInContext(Product? product)
    {
        var list = product is null ? new List<Product>() : new List<Product> { product };
        _contextMock.Setup(x => x.Products).ReturnsDbSet(list);
    }

    private void SetupTrackedProduct(Product? product)
    {
        _uowMock.Setup(x => x.GetTrackedByIdAsync<Product>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
    }

    private void SetupEmptyImages()
    {
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<ProductImage>());
    }

    private void SetupEmptyInventoryRecords()
    {
        _contextMock.Setup(x => x.InventoryRecords).ReturnsDbSet(new List<InventoryRecord>());
    }

    private void SetupEmptyOffers()
    {
        _contextMock.Setup(x => x.Offers).ReturnsDbSet(new List<Offer>());
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        var command = new DeleteProductCommand(ProductId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ProductNotFound_ThrowsNotFoundException()
    {
        SetupProductInContext(null);
        var command = new DeleteProductCommand(ProductId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ProductBelongsToDifferentRetailer_ThrowsNotFoundException()
    {
        var product = BuildProduct(ProductId, Guid.NewGuid());
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product>());

        var command = new DeleteProductCommand(ProductId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_SoftDeletesProduct()
    {
        var product = BuildProduct();
        SetupProductInContext(product);
        SetupTrackedProduct(product);
        SetupEmptyImages();
        SetupEmptyInventoryRecords();
        SetupEmptyOffers();

        var command = new DeleteProductCommand(ProductId);
        await _sut.Handle(command, default);

        product.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidCommand_SoftDeletesAllProductImages()
    {
        var product = BuildProduct();
        SetupProductInContext(product);
        SetupTrackedProduct(product);

        var image1 = ProductImage.Create(ProductId, "https://example.com/img1.jpg", 0);
        var image2 = ProductImage.Create(ProductId, "https://example.com/img2.jpg", 1);
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<ProductImage> { image1, image2 });

        SetupEmptyInventoryRecords();
        SetupEmptyOffers();

        var command = new DeleteProductCommand(ProductId);
        await _sut.Handle(command, default);

        image1.IsDeleted.Should().BeTrue();
        image2.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidCommand_SoftDeletesInventoryRecord()
    {
        var product = BuildProduct();
        SetupProductInContext(product);
        SetupTrackedProduct(product);
        SetupEmptyImages();

        var inventory = InventoryRecord.Create(RetailerId, ProductId, "Test Product", 10);
        _contextMock.Setup(x => x.InventoryRecords).ReturnsDbSet(new List<InventoryRecord> { inventory });

        SetupEmptyOffers();

        var command = new DeleteProductCommand(ProductId);
        await _sut.Handle(command, default);

        inventory.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesChanges()
    {
        var product = BuildProduct();
        SetupProductInContext(product);
        SetupTrackedProduct(product);
        SetupEmptyImages();
        SetupEmptyInventoryRecords();
        SetupEmptyOffers();

        var command = new DeleteProductCommand(ProductId);
        await _sut.Handle(command, default);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_InvalidatesCacheKey()
    {
        var product = BuildProduct();
        SetupProductInContext(product);
        SetupTrackedProduct(product);
        SetupEmptyImages();
        SetupEmptyInventoryRecords();
        SetupEmptyOffers();

        var command = new DeleteProductCommand(ProductId);
        await _sut.Handle(command, default);

        _cacheMock.Verify(
            x => x.RemoveAsync(
                CacheKeys.ActiveProductCount(RetailerId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessResult()
    {
        var product = BuildProduct();
        SetupProductInContext(product);
        SetupTrackedProduct(product);
        SetupEmptyImages();
        SetupEmptyInventoryRecords();
        SetupEmptyOffers();

        var command = new DeleteProductCommand(ProductId);
        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ProductAlreadyDeleted_ThrowsNotFoundException()
    {
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product>());

        var command = new DeleteProductCommand(ProductId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_DoesNotSoftDeleteAlreadyDeletedImages()
    {
        var product = BuildProduct();
        SetupProductInContext(product);
        SetupTrackedProduct(product);

        var alreadyDeletedImage = ProductImage.Create(ProductId, "https://example.com/deleted.jpg", 0);
        alreadyDeletedImage.SoftDelete();

        _contextMock.Setup(x => x.ProductImages)
            .ReturnsDbSet(new List<ProductImage> { alreadyDeletedImage });

        SetupEmptyInventoryRecords();
        SetupEmptyOffers();

        var command = new DeleteProductCommand(ProductId);
        await _sut.Handle(command, default);

        product.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidCommand_TrackedProductNotFoundInsideTransaction_ThrowsNotFoundException()
    {
        var product = BuildProduct();
        SetupProductInContext(product);
        SetupTrackedProduct(null);

        var command = new DeleteProductCommand(ProductId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}