using Application.Features.Inventory.Commands.SetLowStockThreshold;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Tests.Unit.Application.Features.Inventory;

public sealed class SetLowStockThresholdCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IInventoryRepository> _inventoryRepoMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<ILogger<SetLowStockThresholdCommandHandler>> _loggerMock = new();
    private readonly Mock<IRepository<InventoryRecord>> _inventoryEntityRepoMock = new();
    private readonly SetLowStockThresholdCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid InventoryRecordId = Guid.NewGuid();

    public SetLowStockThresholdCommandHandlerTests()
    {
        _sut = new SetLowStockThresholdCommandHandler(
            _uowMock.Object,
            _inventoryRepoMock.Object,
            _userServiceMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _uowMock.Setup(x => x.Repository<InventoryRecord>())
            .Returns(_inventoryEntityRepoMock.Object);

        _inventoryEntityRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<InventoryRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private static InventoryRecord BuildInventoryRecord(int currentStock = 50, int lowStockThreshold = 10)
        => InventoryRecord.Create(RetailerId, Guid.NewGuid(), "Test Product", currentStock, lowStockThreshold);

    private static SetLowStockThresholdCommand BuildCommand(int newThreshold = 15)
        => new(InventoryRecordId, newThreshold);

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var result = await _sut.Handle(BuildCommand(newThreshold: 20), CancellationToken.None);

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
        var record = InventoryRecord.Create(differentRetailerId, Guid.NewGuid(), "Other Product", 50, 10);

        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ThresholdAboveMaximumValue_ThrowsBusinessRuleException()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var act = () => _sut.Handle(BuildCommand(newThreshold: 10001), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INVALID_THRESHOLD");
    }

    [Fact]
    public async Task Handle_NegativeThreshold_ThrowsBusinessRuleException()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var act = () => _sut.Handle(BuildCommand(newThreshold: -1), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("INVALID_THRESHOLD");
    }

    [Fact]
    public async Task Handle_ZeroThreshold_ReturnsSuccess()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var result = await _sut.Handle(BuildCommand(newThreshold: 0), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MaximumAllowedThreshold_ReturnsSuccess()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var result = await _sut.Handle(BuildCommand(newThreshold: 10000), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SuccessfulUpdate_CallsUpdateAndSaveChanges()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await _sut.Handle(BuildCommand(newThreshold: 25), CancellationToken.None);

        _inventoryEntityRepoMock.Verify(
            x => x.UpdateAsync(It.IsAny<InventoryRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SuccessfulUpdate_InvalidatesInventoryCache()
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
    public async Task Handle_SuccessfulUpdate_CacheNotInvalidatedBeforeSave()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var saveOrder = new List<string>();

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => saveOrder.Add("save"))
            .ReturnsAsync(1);

        _cacheMock.Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => saveOrder.Add("cache"))
            .Returns(Task.CompletedTask);

        await _sut.Handle(BuildCommand(), CancellationToken.None);

        saveOrder.Should().ContainInOrder("save", "cache");
    }
}