using Application.Common;
using Application.Features.Products.Commands.AddProductImage;
using Application.Features.Products.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;

namespace Tests.Unit.Application.Features.Products;

public sealed class AddProductImageCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly AddProductImageCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private const string UploadedImageUrl = "https://cdn.example.com/products/retailer/image.jpg";

    public AddProductImageCommandHandlerTests()
    {
        _sut = new AddProductImageCommandHandler(
            _uowMock.Object,
            _fileStorageMock.Object,
            _userServiceMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
        _fileStorageMock
            .Setup(x => x.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadedImageUrl);
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

    private static AddProductImageCommand BuildCommand(Guid? productId = null, int displayOrder = 0)
    {
        var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
        var fileUpload = new FileUploadDto(stream, "photo.jpg", "image/jpeg", stream.Length);
        return new AddProductImageCommand(productId ?? ProductId, fileUpload, displayOrder);
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        var command = BuildCommand();

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ProductNotFound_ThrowsNotFoundException()
    {
        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Product>(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var command = BuildCommand();

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ProductBelongsToDifferentRetailer_ThrowsUnauthorizedException()
    {
        var product = BuildProduct(ProductId, Guid.NewGuid());
        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Product>(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildCommand();

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ProductIsDeleted_ThrowsNotFoundException()
    {
        var product = BuildProduct(ProductId, RetailerId, isDeleted: true);
        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Product>(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildCommand();

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_UploadsFileToStorage()
    {
        var product = BuildProduct();
        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Product>(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildCommand();

        await _sut.Handle(command, default);

        _fileStorageMock.Verify(
            x => x.UploadAsync(
                It.IsAny<Stream>(),
                "photo.jpg",
                It.Is<string>(f => f.Contains(RetailerId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_AddsImageToProduct()
    {
        var product = BuildProduct();
        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Product>(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildCommand(displayOrder: 2);

        await _sut.Handle(command, default);

        product.Images.Should().HaveCount(1);
        product.Images.First().ImageUrl.Should().Be(UploadedImageUrl);
        product.Images.First().DisplayOrder.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesChanges()
    {
        var product = BuildProduct();
        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Product>(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildCommand();

        await _sut.Handle(command, default);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessResultWithProductImageDto()
    {
        var product = BuildProduct();
        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Product>(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildCommand(displayOrder: 1);

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeOfType<ProductImageDto>();
        result.Data!.ImageUrl.Should().Be(UploadedImageUrl);
        result.Data.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsImageWithNonEmptyId()
    {
        var product = BuildProduct();
        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Product>(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildCommand();

        var result = await _sut.Handle(command, default);

        result.Data!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_ValidCommand_DefaultDisplayOrderIsZero()
    {
        var product = BuildProduct();
        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Product>(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildCommand(displayOrder: 0);

        var result = await _sut.Handle(command, default);

        result.Data!.DisplayOrder.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ValidCommand_UploadFolderContainsRetailerId()
    {
        var product = BuildProduct();
        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Product>(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildCommand();

        await _sut.Handle(command, default);

        _fileStorageMock.Verify(
            x => x.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.Is<string>(folder => folder == $"products/{RetailerId}"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}