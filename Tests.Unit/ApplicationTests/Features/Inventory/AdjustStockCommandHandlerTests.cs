using Application.Features.Inventory.Commands.AdjustStock;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Tests.Unit.Application.Features.Inventory;

public sealed class AdjustStockCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IInventoryRepository> _inventoryRepoMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<ILogger<AdjustStockCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<StockAdjustment>> _stockAdjRepoMock = new();
    private readonly AdjustStockCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid InventoryRecordId = Guid.NewGuid();

    public AdjustStockCommandHandlerTests()
    {
        _sut = new AdjustStockCommandHandler(
            _uowMock.Object,
            _inventoryRepoMock.Object,
            _mediatorMock.Object,
            _userServiceMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _uowMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> action, CancellationToken ct) => action(ct));

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _uowMock.Setup(x => x.Repository<StockAdjustment>())
            .Returns(_stockAdjRepoMock.Object);

        _stockAdjRepoMock
            .Setup(x => x.AddAsync(It.IsAny<StockAdjustment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockAdjustment sa, CancellationToken _) => sa);
    }

    private static InventoryRecord BuildInventoryRecord(int currentStock = 100, int lowStockThreshold = 10)
        => InventoryRecord.Create(RetailerId, Guid.NewGuid(), "Test Product", currentStock, lowStockThreshold);

    private static AdjustStockCommand BuildCommand(int newQuantity = 50, string? reason = "Restock")
        => new(InventoryRecordId, newQuantity, AdjustmentType.ManualIncrease, reason);

    [Fact]
    public async Task Handle_ValidManualIncreaseRequest_ReturnsSuccess()
    {
        var record = BuildInventoryRecord(currentStock: 30);
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var result = await _sut.Handle(BuildCommand(newQuantity: 50), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NullRetailerId_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_InventoryRecordNotFound_ThrowsNotFoundException()
    {
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryRecord?)null);

        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_InventoryRecordBelongsToDifferentRetailer_ThrowsNotFoundException()
    {
        var differentRetailerId = Guid.NewGuid();
        var record = InventoryRecord.Create(differentRetailerId, Guid.NewGuid(), "Other Product", 100, 10);

        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NegativeNewQuantity_ThrowsBusinessRuleException()
    {
        var record = BuildInventoryRecord(currentStock: 10);
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var act = () => _sut.Handle(BuildCommand(newQuantity: -1), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("STOCK_FLOOR_VIOLATION");
    }

    [Fact]
    public async Task Handle_StockAtOrBelowThreshold_PublishesLowStockWarningEvent()
    {
        var record = BuildInventoryRecord(currentStock: 50, lowStockThreshold: 20);
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await _sut.Handle(BuildCommand(newQuantity: 5), CancellationToken.None);

        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<LowStockWarningEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StockAboveThreshold_DoesNotPublishLowStockWarningEvent()
    {
        var record = BuildInventoryRecord(currentStock: 5, lowStockThreshold: 10);
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await _sut.Handle(BuildCommand(newQuantity: 100), CancellationToken.None);

        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<LowStockWarningEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_StockExactlyAtThreshold_PublishesLowStockWarningEvent()
    {
        var record = BuildInventoryRecord(currentStock: 50, lowStockThreshold: 10);
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await _sut.Handle(BuildCommand(newQuantity: 10), CancellationToken.None);

        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<LowStockWarningEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_FirstAttemptConcurrencyConflict_RetriesAndSucceeds()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var callCount = 0;
        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct =>
            {
                callCount++;
                return callCount == 1
                    ? Task.FromException<int>(new DbUpdateConcurrencyException("Concurrency conflict"))
                    : Task.FromResult(1);
            });

        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_BothAttemptsConcurrencyConflict_ThrowsConflictException()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict"));

        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_SuccessfulAdjustment_InvalidatesInventoryCache()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await _sut.Handle(BuildCommand(), CancellationToken.None);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(
                It.Is<string>(key => key.Contains(RetailerId.ToString("N"))),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SuccessfulAdjustment_PersistsStockAdjustmentRecord()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await _sut.Handle(BuildCommand(), CancellationToken.None);

        _stockAdjRepoMock.Verify(
            x => x.AddAsync(It.IsAny<StockAdjustment>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ZeroNewQuantity_SetsStockToZeroSuccessfully()
    {
        var record = BuildInventoryRecord(currentStock: 100);
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var result = await _sut.Handle(BuildCommand(newQuantity: 0), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ConcurrencyExceptionAfterCacheInvalidation_CacheNotInvalidatedOnFailure()
    {
        _uowMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Conflict"));

        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}