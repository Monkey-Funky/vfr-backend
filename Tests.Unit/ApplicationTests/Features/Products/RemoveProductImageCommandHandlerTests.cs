using Application.Features.Products.Commands.RemoveProductImage;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Moq.EntityFrameworkCore;

namespace Tests.Unit.Application.Features.Products;

public sealed class RemoveProductImageCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly RemoveProductImageCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid ImageId = Guid.NewGuid();
    private const string ImageUrl = "https://cdn.example.com/products/image.jpg";

    public RemoveProductImageCommandHandlerTests()
    {
        _sut = new RemoveProductImageCommandHandler(
            _uowMock.Object,
            _fileStorageMock.Object,
            _userServiceMock.Object,
            _contextMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
        _fileStorageMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private Product BuildProduct(Guid? retailerId = null)
    {
        var product = Product.Create(retailerId ?? RetailerId, "Test Product");
        typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(product, ProductId);
        return product;
    }

    private ProductImage BuildImage(Guid? imageId = null, bool isDeleted = false)
    {
        var image = ProductImage.Create(ProductId, ImageUrl, 0);
        typeof(ProductImage).GetProperty(nameof(ProductImage.Id))!.SetValue(image, imageId ?? ImageId);
        if (isDeleted) image.SoftDelete();
        return image;
    }

    private void SetupProductInContext(bool exists, Guid? retailerId = null)
    {
        if (exists)
        {
            var product = BuildProduct(retailerId);
            _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product> { product });
        }
        else
        {
            _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Product>());
        }
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        var command = new RemoveProductImageCommand(ProductId, ImageId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ProductNotFound_ThrowsNotFoundException()
    {
        SetupProductInContext(false);
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<ProductImage>());

        var command = new RemoveProductImageCommand(ProductId, ImageId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ProductBelongsToDifferentRetailer_ThrowsNotFoundException()
    {
        SetupProductInContext(false);
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<ProductImage>());

        var command = new RemoveProductImageCommand(ProductId, ImageId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ImageNotFound_ThrowsNotFoundException()
    {
        SetupProductInContext(true);
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<ProductImage>());

        var command = new RemoveProductImageCommand(ProductId, ImageId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AlreadySoftDeletedImage_ThrowsNotFoundException()
    {
        SetupProductInContext(true);
        var deletedImage = BuildImage(isDeleted: true);
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<ProductImage> { deletedImage });

        var command = new RemoveProductImageCommand(ProductId, ImageId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_SoftDeletesImage()
    {
        SetupProductInContext(true);
        var image = BuildImage();
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<ProductImage> { image });

        var command = new RemoveProductImageCommand(ProductId, ImageId);
        await _sut.Handle(command, default);

        image.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesChanges()
    {
        SetupProductInContext(true);
        var image = BuildImage();
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<ProductImage> { image });

        var command = new RemoveProductImageCommand(ProductId, ImageId);
        await _sut.Handle(command, default);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_DeletesImageFromStorage()
    {
        SetupProductInContext(true);
        var image = BuildImage();
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<ProductImage> { image });

        var command = new RemoveProductImageCommand(ProductId, ImageId);
        await _sut.Handle(command, default);

        _fileStorageMock.Verify(
            x => x.DeleteAsync(ImageUrl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StorageDeleteThrowsExternalServiceException_StillReturnsSuccess()
    {
        SetupProductInContext(true);
        var image = BuildImage();
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<ProductImage> { image });

        _fileStorageMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalServiceException("S3", "Connection timeout."));

        var command = new RemoveProductImageCommand(ProductId, ImageId);
        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        image.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessResult()
    {
        SetupProductInContext(true);
        var image = BuildImage();
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<ProductImage> { image });

        var command = new RemoveProductImageCommand(ProductId, ImageId);
        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ValidCommand_DbIsSoftDeletedBeforeStorageDelete()
    {
        SetupProductInContext(true);
        var image = BuildImage();
        _contextMock.Setup(x => x.ProductImages).ReturnsDbSet(new List<ProductImage> { image });

        var callOrder = new List<string>();

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("db"))
            .ReturnsAsync(1);

        _fileStorageMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("storage"))
            .Returns(Task.CompletedTask);

        var command = new RemoveProductImageCommand(ProductId, ImageId);
        await _sut.Handle(command, default);

        callOrder.Should().Equal("db", "storage");
    }
}