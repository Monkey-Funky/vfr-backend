using Application.Features.Products.Commands.ToggleProductStatus;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Moq.EntityFrameworkCore;
using Shared.Constants;

namespace Tests.Unit.Application.Features.Products;

public sealed class ToggleProductStatusCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly ToggleProductStatusCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public ToggleProductStatusCommandHandlerTests()
    {
        _sut = new ToggleProductStatusCommandHandler(
            _uowMock.Object,
            _userServiceMock.Object,
            _contextMock.Object,
            _cacheMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private Product BuildProduct(
        Guid? id = null,
        Guid? retailerId = null,
        string status = ProductStatus.Active,
        bool isDeleted = false)
    {
        var product = Product.Create(retailerId ?? RetailerId, "Test Product", status: status);
        var pid = id ?? ProductId;
        typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(product, pid);
        if (isDeleted) product.SoftDelete();
        return product;
    }

    private void SetupTrackedProduct(Product? product)
    {
        _uowMock
            .Setup(x => x.GetTrackedByIdAsync<Product>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
    }

    private void SetupInventoryStock(int stock)
    {
        var inventory = InventoryRecord.Create(RetailerId, ProductId, "Test Product", stock);
        _contextMock.Setup(x => x.InventoryRecords).ReturnsDbSet(new List<InventoryRecord> { inventory });
    }

    private void SetupNoInventory()
    {
        _contextMock.Setup(x => x.InventoryRecords).ReturnsDbSet(new List<InventoryRecord>());
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        var command = new ToggleProductStatusCommand(ProductId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ProductNotFound_ThrowsNotFoundException()
    {
        SetupTrackedProduct(null);
        var command = new ToggleProductStatusCommand(ProductId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ProductBelongsToDifferentRetailer_ThrowsUnauthorizedException()
    {
        var product = BuildProduct(ProductId, Guid.NewGuid());
        SetupTrackedProduct(product);

        var command = new ToggleProductStatusCommand(ProductId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ProductIsDeleted_ThrowsNotFoundException()
    {
        var product = BuildProduct(ProductId, RetailerId, isDeleted: true);
        SetupTrackedProduct(product);

        var command = new ToggleProductStatusCommand(ProductId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ActiveProduct_TogglesStatusToInactive()
    {
        var product = BuildProduct(status: ProductStatus.Active);
        SetupTrackedProduct(product);

        var command = new ToggleProductStatusCommand(ProductId);
        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(ProductStatus.Inactive);
        product.Status.Should().Be(ProductStatus.Inactive);
    }

    [Fact]
    public async Task Handle_InactiveProduct_TogglesStatusToActive()
    {
        var product = BuildProduct(status: ProductStatus.Inactive);
        SetupTrackedProduct(product);

        var command = new ToggleProductStatusCommand(ProductId);
        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(ProductStatus.Active);
        product.Status.Should().Be(ProductStatus.Active);
    }

    [Fact]
    public async Task Handle_DraftProduct_TogglesStatusToActive()
    {
        var product = BuildProduct(status: ProductStatus.Draft);
        SetupTrackedProduct(product);

        var command = new ToggleProductStatusCommand(ProductId);
        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(ProductStatus.Active);
        product.Status.Should().Be(ProductStatus.Active);
    }

    [Fact]
    public async Task Handle_OutOfStockProductWithZeroStock_ThrowsBusinessRuleException()
    {
        var product = BuildProduct(status: ProductStatus.OutOfStock);
        SetupTrackedProduct(product);
        SetupInventoryStock(0);

        var command = new ToggleProductStatusCommand(ProductId);

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task Handle_OutOfStockProductWithNegativeStock_ThrowsBusinessRuleException()
    {
        var product = BuildProduct(status: ProductStatus.OutOfStock);
        SetupTrackedProduct(product);
        SetupInventoryStock(0);

        var command = new ToggleProductStatusCommand(ProductId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Handle_OutOfStockProductWithPositiveStock_TogglesStatusToActive()
    {
        var product = BuildProduct(status: ProductStatus.OutOfStock);
        SetupTrackedProduct(product);
        SetupInventoryStock(5);

        var command = new ToggleProductStatusCommand(ProductId);
        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(ProductStatus.Active);
    }

    [Fact]
    public async Task Handle_OutOfStockProductWithNoInventoryRecord_ThrowsBusinessRuleException()
    {
        var product = BuildProduct(status: ProductStatus.OutOfStock);
        SetupTrackedProduct(product);
        SetupNoInventory();

        var command = new ToggleProductStatusCommand(ProductId);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesChanges()
    {
        var product = BuildProduct(status: ProductStatus.Active);
        SetupTrackedProduct(product);

        var command = new ToggleProductStatusCommand(ProductId);
        await _sut.Handle(command, default);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_InvalidatesActiveProductCountCache()
    {
        var product = BuildProduct(status: ProductStatus.Active);
        SetupTrackedProduct(product);

        var command = new ToggleProductStatusCommand(ProductId);
        await _sut.Handle(command, default);

        _cacheMock.Verify(
            x => x.RemoveAsync(
                CacheKeys.ActiveProductCount(RetailerId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessResultWithNewStatus()
    {
        var product = BuildProduct(status: ProductStatus.Inactive);
        SetupTrackedProduct(product);

        var command = new ToggleProductStatusCommand(ProductId);
        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(ProductStatus.Active);
        result.Message.Should().Contain(ProductStatus.Active);
    }
}