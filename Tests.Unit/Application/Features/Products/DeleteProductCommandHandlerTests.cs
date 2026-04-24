// tests/Tests.Unit/Application/Features/Products/DeleteProductCommandHandlerTests.cs
using Application.Features.Products.Commands.DeleteProduct;
using Application.Interfaces;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Domain.Enums;
using Domain.Enums.Product;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using Shared.DTOs;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Products;

public sealed class DeleteProductCommandHandlerTests : TestBase
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly DeleteProductCommandHandler _handler;

    public DeleteProductCommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _currentUserMock = MockRepository.Create<ICurrentUserService>();
        _contextMock = MockRepository.Create<IApplicationDbContext>();
        _cacheMock = MockRepository.Create<ICacheService>();

        _handler = new DeleteProductCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserMock.Object,
            _contextMock.Object,
            _cacheMock.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Product BuildActiveProduct(Guid retailerId)
    {
        var p = Product.Create(retailerId, "Test Product", null, null, null,
            99.99m, "EGP", null, ProductStatus.Active);
        return p;
    }

    [Fact]
    public async Task Handle_ProductNotBelongingToRetailer_ThrowsNotFoundException()
    {
        // Arrange — Products.AnyAsync returns false (product not found for this retailer = IDOR guard)
        Guid retailerId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();

        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        var emptyProductDbSet = MockDbSetFactory.Create(new List<Product>());
        _contextMock.Setup(c => c.Products).Returns(emptyProductDbSet.Object);

        var command = new DeleteProductCommand(productId);

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Product*");
    }

    [Fact]
    public async Task Handle_ValidDelete_SoftDeletesProductAndImagesAndInventoryRecord()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        var product = BuildActiveProduct(retailerId);

        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        // IDOR guard passes: Products.AnyAsync returns true
        var productDbSet = MockDbSetFactory.Create(new List<Product> { product });
        _contextMock.Setup(c => c.Products).Returns(productDbSet.Object);

        // ExecuteInTransactionAsync — invoke the lambda
        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Func<CancellationToken, Task>, CancellationToken>(
                (action, ct) => action(ct).GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        // GetTrackedByIdAsync returns the same product
        _unitOfWorkMock
            .Setup(u => u.GetTrackedByIdAsync<Product>(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // Images — create a ProductImage linked to this product
        var image = ProductImage.Create(product.Id, "https://cdn.example.com/img.jpg", 0);
        var imageDbSet = MockDbSetFactory.Create(new List<ProductImage> { image });
        _contextMock.Setup(c => c.ProductImages).Returns(imageDbSet.Object);

        // InventoryRecord
        var inventory = InventoryRecord.Create(retailerId, product.Id, "Test Product", 10, 5);
        var invDbSet = MockDbSetFactory.Create(new List<InventoryRecord> { inventory });
        _contextMock.Setup(c => c.InventoryRecords).Returns(invDbSet.Object);

        // Offers (empty — no offers to inactivate)
        var emptyOffers = MockDbSetFactory.Create(new List<Offer>());
        _contextMock.Setup(c => c.Offers).Returns(emptyOffers.Object);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Verifiable();

        _cacheMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var command = new DeleteProductCommand(product.Id);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Product must be soft-deleted
        product.IsDeleted.Should().BeTrue(
            "DeleteProductCommandHandler must call product.SoftDelete() inside the transaction");

        // Image must be soft-deleted
        image.IsDeleted.Should().BeTrue(
            "all ProductImages belonging to the product must be soft-deleted");

        // InventoryRecord must be soft-deleted
        inventory.IsDeleted.Should().BeTrue(
            "the InventoryRecord associated with the product must be soft-deleted");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(
            c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}