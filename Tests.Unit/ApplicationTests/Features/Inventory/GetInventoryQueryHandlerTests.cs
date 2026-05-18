using Application.Features.Inventory.DTOs;
using Application.Features.Inventory.Queries.GetInventory;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Shared.DTOs;

namespace Tests.Unit.Application.Features.Inventory;

public sealed class GetInventoryQueryHandlerTests
{
    private readonly Mock<IInventoryRepository> _inventoryRepoMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly GetInventoryQueryHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OtherRetailerId = Guid.NewGuid();

    public GetInventoryQueryHandlerTests()
    {
        _sut = new GetInventoryQueryHandler(
            _inventoryRepoMock.Object,
            _userServiceMock.Object,
            _cacheMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _cacheMock
            .Setup(x => x.GetAsync<PagedResult<InventoryDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResult<InventoryDto>?)null);

        _cacheMock
            .Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<PagedResult<InventoryDto>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static InventoryRecord BuildRecord(
        Guid? retailerId = null,
        int currentStock = 100,
        int lowStockThreshold = 10,
        string? name = null)
        => InventoryRecord.Create(
            retailerId ?? RetailerId,
            Guid.NewGuid(),
            name ?? "Test Product",
            currentStock,
            lowStockThreshold);

    private void SetupRepo(
        IReadOnlyList<InventoryRecord> items,
        int totalCount,
        string? productNameFilter = null,
        bool sortBySoldDesc = false,
        int page = 1,
        int size = 20)
    {
        _inventoryRepoMock
            .Setup(r => r.GetPagedAsync(
                RetailerId,
                productNameFilter,
                sortBySoldDesc,
                page,
                size,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, totalCount));
    }

    [Fact]
    public async Task Handle_NoInventory_ReturnsEmptyPagedResult()
    {
        SetupRepo(new List<InventoryRecord>(), 0);

        var result = await _sut.Handle(new GetInventoryQuery(1, 20), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_InventoryExists_ReturnsPagedList()
    {
        var records = new List<InventoryRecord>
        {
            BuildRecord(currentStock: 50),
            BuildRecord(currentStock: 80),
        };
        SetupRepo(records, 2);

        var result = await _sut.Handle(new GetInventoryQuery(1, 20), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.Should().AllSatisfy(item => item.Should().NotBeNull());
    }

    [Fact]
    public async Task Handle_FilterByLowStock_ReturnsLowStockItems()
    {
        var lowStockRecord = BuildRecord(currentStock: 5, lowStockThreshold: 10);
        var records = new List<InventoryRecord> { lowStockRecord };
        SetupRepo(records, 1);

        var result = await _sut.Handle(new GetInventoryQuery(1, 20), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be(InventoryStatus.LowStock);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RetailerSeesOnlyOwnInventory()
    {
        var ownRecord = BuildRecord(retailerId: RetailerId);
        SetupRepo(new List<InventoryRecord> { ownRecord }, 1);

        await _sut.Handle(new GetInventoryQuery(1, 20), CancellationToken.None);

        _inventoryRepoMock.Verify(r => r.GetPagedAsync(
            RetailerId,
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _inventoryRepoMock.Verify(r => r.GetPagedAsync(
            OtherRetailerId,
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullRetailerId_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new GetInventoryQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_CacheHit_DoesNotCallRepository()
    {
        var cachedResult = new PagedResult<InventoryDto>
        {
            Items = [],
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20
        };

        _cacheMock
            .Setup(x => x.GetAsync<PagedResult<InventoryDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResult);

        var result = await _sut.Handle(new GetInventoryQuery(1, 20), CancellationToken.None);

        result.Should().BeSameAs(cachedResult);
        _inventoryRepoMock.Verify(r => r.GetPagedAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<bool>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CacheMiss_StoresResultInCache()
    {
        SetupRepo(new List<InventoryRecord> { BuildRecord() }, 1);

        await _sut.Handle(new GetInventoryQuery(1, 20), CancellationToken.None);

        _cacheMock.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<PagedResult<InventoryDto>>(),
            TimeSpan.FromMinutes(5),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}