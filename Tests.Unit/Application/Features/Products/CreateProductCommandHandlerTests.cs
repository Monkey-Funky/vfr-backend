using Application.Features.Products.Commands.CreateProduct;
using Application.Features.Products.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Domain.Enums.Product;
using Application.Common;
using global::Domain.Exceptions;
using Moq.EntityFrameworkCore;

namespace Tests.Unit.Application.Features.Products;

public sealed class CreateProductCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IProductRepository> _productRepoMock = new();
    private readonly Mock<ISubscriptionService> _subServiceMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly CreateProductCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();

    public CreateProductCommandHandlerTests()
    {
        _sut = new CreateProductCommandHandler(
            _uowMock.Object,
            _productRepoMock.Object,
            _subServiceMock.Object,
            _fileStorageMock.Object,
            _userServiceMock.Object,
            _contextMock.Object,
            _cacheMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
        
        _uowMock.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> action, CancellationToken ct) => action(ct));
    }

    private CurrentPlanInfo CreatePlanInfo(int? maxProducts = null) =>
        new(Guid.NewGuid(), "Plan", "Tier", maxProducts, 100);

    [Fact]
    public async Task Handle_ValidRequest_CreatesProductAndInventory()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var category = Category.Create(RetailerId, "Electronics", "Desc", "icon", "#000");
        // Reflection to set ID if needed, but the handler uses AnyAsync which we can mock
        
        var command = new CreateProductCommand(
            Name: "Test Product",
            Description: "Desc",
            CategoryId: categoryId,
            SubCategoryId: null,
            Price: 99.99m,
            Currency: "USD",
            Barcode: "123456",
            InitialQuantity: 100,
            Status: ProductStatus.Active,
            Images: null
        );

        _contextMock.Setup(x => x.Categories).ReturnsDbSet(new List<Category> { category });
        // Set ID via reflection because Category.Id is private set and no ID in constructor
        typeof(Category).GetProperty(nameof(Category.Id))!.SetValue(category, categoryId);

        var productRepo = new Mock<IRepository<Product>>();
        _uowMock.Setup(x => x.Repository<Product>()).Returns(productRepo.Object);
        
        var inventoryRepo = new Mock<IRepository<InventoryRecord>>();
        _uowMock.Setup(x => x.Repository<InventoryRecord>()).Returns(inventoryRepo.Object);

        _subServiceMock.Setup(x => x.GetCurrentPlanAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePlanInfo(10));

        _productRepoMock.Setup(x => x.GetActiveProductCountByRetailerAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _productRepoMock.Setup(x => x.GetByBarcodeAsync(RetailerId, "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _sut.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        productRepo.Verify(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        inventoryRepo.Verify(x => x.AddAsync(It.IsAny<InventoryRecord>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateBarcode_ThrowsConflictException()
    {
        // Arrange
        var command = new CreateProductCommand(
            Name: "Test Product",
            Description: null,
            CategoryId: null,
            SubCategoryId: null,
            Price: null,
            Currency: "USD",
            Barcode: "123456",
            InitialQuantity: 0,
            Status: ProductStatus.Active,
            Images: null
        );

        _productRepoMock.Setup(x => x.GetByBarcodeAsync(RetailerId, "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Product.Create(RetailerId, "Existing", "", null, null, 10, "USD", "123456", ProductStatus.Active));

        // Act
        var act = () => _sut.Handle(command, default);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_PlanLimitExceeded_ThrowsBusinessRuleException()
    {
        // Arrange
        var command = new CreateProductCommand(
            Name: "New Product",
            Description: null,
            CategoryId: null,
            SubCategoryId: null,
            Price: null,
            Currency: "USD",
            Barcode: null,
            InitialQuantity: 0,
            Status: ProductStatus.Active,
            Images: null
        );

        _subServiceMock.Setup(x => x.GetCurrentPlanAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePlanInfo(1));

        _productRepoMock.Setup(x => x.GetActiveProductCountByRetailerAsync(RetailerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var act = () => _sut.Handle(command, default);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("PRODUCT_LIMIT_EXCEEDED");
    }
}
