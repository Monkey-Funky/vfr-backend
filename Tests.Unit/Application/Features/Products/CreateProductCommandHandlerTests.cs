// tests/Tests.Unit/Application/Features/Products/CreateProductCommandHandlerTests.cs
using Application.Features.Products.Commands.CreateProduct;
using Application.Features.Products.DTOs;
using Application.Interfaces;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using Shared.DTOs;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Products;

public sealed class CreateProductCommandHandlerTests : TestBase
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IProductRepository> _productRepoMock;
    private readonly Mock<ISubscriptionService> _subscriptionMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly CreateProductCommandHandler _handler;

    public CreateProductCommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _productRepoMock = MockRepository.Create<IProductRepository>();
        _subscriptionMock = MockRepository.Create<ISubscriptionService>();
        _fileStorageMock = MockRepository.Create<IFileStorageService>();
        _currentUserMock = MockRepository.Create<ICurrentUserService>();
        _contextMock = MockRepository.Create<IApplicationDbContext>();
        _cacheMock = MockRepository.Create<ICacheService>();

        _handler = new CreateProductCommandHandler(
            _unitOfWorkMock.Object,
            _productRepoMock.Object,
            _subscriptionMock.Object,
            _fileStorageMock.Object,
            _currentUserMock.Object,
            _contextMock.Object,
            _cacheMock.Object);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_FreePlanWith50ActiveProducts_ThrowsBusinessRuleException()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        // No category or subcategory to validate
        var emptyCategories = MockDbSetFactory.Create(new List<Category>());
        _contextMock.Setup(c => c.Categories).Returns(emptyCategories.Object);

        // No barcode clash
        _productRepoMock
            .Setup(r => r.GetByBarcodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // ExecuteInTransactionAsync — invoke the lambda (plan limit check happens inside)
        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Func<CancellationToken, Task>, CancellationToken>(
                (action, ct) => action(ct).GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        // Free plan with MaxActiveProducts = 50
        _subscriptionMock
            .Setup(s => s.GetCurrentPlanAsync(retailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentPlanInfo(Guid.NewGuid(), "Free", "Free", 50, null));

        // Already at 50 active products — limit reached
        _productRepoMock
            .Setup(r => r.GetActiveProductCountByRetailerAsync(retailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        var command = new CreateProductCommand(
            "New Product", null, null, null, 29.99m, "EGP", null, 0, "Active", null);

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*PRODUCT_LIMIT_EXCEEDED*");
    }

    [Fact]
    public async Task Handle_FreePlanUnderLimit_CreatesProductAndInventoryRecordSuccessfully()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        var emptyCategories = MockDbSetFactory.Create(new List<Category>());
        _contextMock.Setup(c => c.Categories).Returns(emptyCategories.Object);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Func<CancellationToken, Task>, CancellationToken>(
                (action, ct) => action(ct).GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        // Free plan: 50 limit, currently 10 active
        _subscriptionMock
            .Setup(s => s.GetCurrentPlanAsync(retailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentPlanInfo(Guid.NewGuid(), "Free", "Free", 50, null));

        _productRepoMock
            .Setup(r => r.GetActiveProductCountByRetailerAsync(retailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        // FIX: IRepository<T>.AddAsync returns Task<T>, not Task.
        //      Both Product and InventoryRecord inner-repo mocks must use Returns<T, CancellationToken>
        //      so the return type is Task<T> rather than Task, eliminating the CS1503 argument error.

        var productRepoInner = new Mock<IRepository<Product>>(MockBehavior.Strict);
        productRepoInner
            .Setup(r => r.AddAsync(
                It.IsAny<Product>(),
                It.IsAny<CancellationToken>()))
            .Returns<Product, CancellationToken>(
                (product, _) => Task.FromResult(product));

        var inventoryRepoInner = new Mock<IRepository<InventoryRecord>>(MockBehavior.Strict);
        inventoryRepoInner
            .Setup(r => r.AddAsync(
                It.IsAny<InventoryRecord>(),
                It.IsAny<CancellationToken>()))
            .Returns<InventoryRecord, CancellationToken>(
                (record, _) => Task.FromResult(record));

        _unitOfWorkMock.Setup(u => u.Repository<Product>())
            .Returns(productRepoInner.Object);
        _unitOfWorkMock.Setup(u => u.Repository<InventoryRecord>())
            .Returns(inventoryRepoInner.Object);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Category/subcategory/inventory DbSets needed for the post-create projection
        var emptyCategories2 = MockDbSetFactory.Create(new List<Category>());
        _contextMock.Setup(c => c.Categories).Returns(emptyCategories2.Object);

        var emptySubCats = MockDbSetFactory.Create(new List<SubCategory>());
        _contextMock.Setup(c => c.SubCategories).Returns(emptySubCats.Object);

        var emptyInventory = MockDbSetFactory.Create(new List<InventoryRecord>());
        _contextMock.Setup(c => c.InventoryRecords).Returns(emptyInventory.Object);

        var command = new CreateProductCommand(
            "Shirt", "A nice shirt", null, null, 49.99m, "EGP", null, 5, "Active", null);

        // Act
        Result<ProductDetailDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Shirt");
    }
}