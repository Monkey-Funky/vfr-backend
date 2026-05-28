using Application.Features.Inventory.DTOs;
using Application.Features.Inventory.Queries.GetInventoryByProductId;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Tests.Unit.ApplicationTests.Features.Inventory;

public sealed class GetInventoryByProductIdQueryHandlerTests
{
    private readonly Mock<IInventoryRepository> _inventoryRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly GetInventoryByProductIdQueryHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OtherRetailerId = Guid.NewGuid();

    public GetInventoryByProductIdQueryHandlerTests()
    {
        _sut = new GetInventoryByProductIdQueryHandler(
            _inventoryRepoMock.Object,
            _currentUserServiceMock.Object,
            _cacheServiceMock.Object);

        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _cacheServiceMock
            .Setup(x => x.GetAsync<InventoryDetailDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryDetailDto?)null);

        _cacheServiceMock
            .Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<InventoryDetailDto>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static InventoryRecord CreateInventoryRecord(Guid retailerId, Guid productId)
        => InventoryRecord.Create(retailerId, productId, "Product Name", 50, 10);

    [Fact]
    public async Task Handle_ProductNotFound_ThrowsNotFoundException()
    {
        var productId = Guid.NewGuid();
        _inventoryRepoMock
            .Setup(x => x.GetByProductIdAsync(RetailerId, productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryRecord?)null);

        var act = () => _sut.Handle(new GetInventoryByProductIdQuery(productId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidProductId_ReturnsInventoryDetails()
    {
        var productId = Guid.NewGuid();
        var record = CreateInventoryRecord(RetailerId, productId);

        _inventoryRepoMock
            .Setup(x => x.GetByProductIdAsync(RetailerId, productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var result = await _sut.Handle(new GetInventoryByProductIdQuery(productId), CancellationToken.None);

        result.Should().NotBeNull();
        result.ProductId.Should().Be(productId);
        result.RetailerId.Should().Be(RetailerId);
        result.CurrentStock.Should().Be(50);
        result.LowStockThreshold.Should().Be(10);
    }

    [Fact]
    public async Task Handle_RetailerSeesOnlyOwnInventory_ThrowsNotFoundException()
    {
        var productId = Guid.NewGuid();
        var otherRecord = CreateInventoryRecord(OtherRetailerId, productId);

        _inventoryRepoMock
            .Setup(x => x.GetByProductIdAsync(RetailerId, productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherRecord);

        var act = () => _sut.Handle(new GetInventoryByProductIdQuery(productId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedResult()
    {
        var productId = Guid.NewGuid();
        var cachedDto = new InventoryDetailDto(
            Guid.NewGuid(), RetailerId, productId, "Cached Product",
            20, 5, 10, "InStock", DateTime.UtcNow, null, []);

        _cacheServiceMock
            .Setup(x => x.GetAsync<InventoryDetailDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDto);

        var result = await _sut.Handle(new GetInventoryByProductIdQuery(productId), CancellationToken.None);

        result.Should().Be(cachedDto);
        _inventoryRepoMock.Verify(
            x => x.GetByProductIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CacheMiss_StoresResultInCache()
    {
        var productId = Guid.NewGuid();
        var record = CreateInventoryRecord(RetailerId, productId);

        _inventoryRepoMock
            .Setup(x => x.GetByProductIdAsync(RetailerId, productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await _sut.Handle(new GetInventoryByProductIdQuery(productId), CancellationToken.None);

        _cacheServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<InventoryDetailDto>(),
                It.Is<TimeSpan?>(t => t == TimeSpan.FromMinutes(5)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MissingRetailerId_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new GetInventoryByProductIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}