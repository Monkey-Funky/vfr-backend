using Application.Features.Inventory.Commands.DeleteInventoryRecord;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Tests.Unit.Application.Features.Inventory;

public sealed class DeleteInventoryRecordCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IInventoryRepository> _inventoryRepoMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<InventoryRecord>> _inventoryEntityRepoMock = new();
    private readonly DeleteInventoryRecordCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid InventoryRecordId = Guid.NewGuid();

    public DeleteInventoryRecordCommandHandlerTests()
    {
        _sut = new DeleteInventoryRecordCommandHandler(
            _uowMock.Object,
            _inventoryRepoMock.Object,
            _userServiceMock.Object,
            _cacheMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _uowMock.Setup(x => x.Repository<InventoryRecord>())
            .Returns(_inventoryEntityRepoMock.Object);

        _inventoryEntityRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<InventoryRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private static InventoryRecord BuildInventoryRecord()
        => InventoryRecord.Create(RetailerId, Guid.NewGuid(), "Test Product", 100, 10);

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var result = await _sut.Handle(new DeleteInventoryRecordCommand(InventoryRecordId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NullRetailerId_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new DeleteInventoryRecordCommand(InventoryRecordId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_InventoryRecordNotFound_ThrowsNotFoundException()
    {
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryRecord?)null);

        var act = () => _sut.Handle(new DeleteInventoryRecordCommand(InventoryRecordId), CancellationToken.None);

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

        var act = () => _sut.Handle(new DeleteInventoryRecordCommand(InventoryRecordId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SuccessfulDelete_SetsIsDeletedToTrue()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        InventoryRecord? capturedRecord = null;
        _inventoryEntityRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<InventoryRecord>(), It.IsAny<CancellationToken>()))
            .Callback<InventoryRecord, CancellationToken>((r, _) => capturedRecord = r)
            .Returns(Task.CompletedTask);

        await _sut.Handle(new DeleteInventoryRecordCommand(InventoryRecordId), CancellationToken.None);

        capturedRecord.Should().NotBeNull();
        capturedRecord!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SuccessfulDelete_CallsUpdateAndSaveChanges()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await _sut.Handle(new DeleteInventoryRecordCommand(InventoryRecordId), CancellationToken.None);

        _inventoryEntityRepoMock.Verify(
            x => x.UpdateAsync(It.IsAny<InventoryRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SuccessfulDelete_InvalidatesInventoryCache()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await _sut.Handle(new DeleteInventoryRecordCommand(InventoryRecordId), CancellationToken.None);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(
                It.Is<string>(key => key.Contains(RetailerId.ToString("N"))),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SuccessfulDelete_DoesNotDeleteParentProduct()
    {
        var productId = Guid.NewGuid();
        var record = InventoryRecord.Create(RetailerId, productId, "Test Product", 100, 10);
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        await _sut.Handle(new DeleteInventoryRecordCommand(InventoryRecordId), CancellationToken.None);

        _uowMock.Verify(
            x => x.Repository<Domain.Entities.Retailer.Product>(),
            Times.Never);
    }

    [Fact]
    public async Task Handle_FailedSave_DoesNotInvalidateCache()
    {
        var record = BuildInventoryRecord();
        _inventoryRepoMock
            .Setup(x => x.GetTrackedByIdAsync(RetailerId, InventoryRecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var act = () => _sut.Handle(new DeleteInventoryRecordCommand(InventoryRecordId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}