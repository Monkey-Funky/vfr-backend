using Application.Features.Customer.Wardrobe.Commands.DeleteCollection;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Moq.EntityFrameworkCore;

namespace Tests.Unit.Application.Features.Customer.Wardrobe;

public sealed class DeleteCollectionCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly DeleteCollectionCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();

    public DeleteCollectionCommandHandlerTests()
    {
        _sut = new DeleteCollectionCommandHandler(
            _contextMock.Object,
            _currentUserServiceMock.Object);

        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
    }

    private static WardrobeCollection CreateCollection(Guid customerId, Guid collectionId)
    {
        var col = WardrobeCollection.Create(customerId, "My Collection");
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(col, collectionId);
        return col;
    }

    private static WardrobeCollectionItem CreateItem(Guid collectionId, Guid favoriteId)
    {
        var item = WardrobeCollectionItem.Create(collectionId, favoriteId);
        return item;
    }

    [Fact]
    public async Task Handle_CustomerIdIsNull_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new DeleteCollectionCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*authenticated customers*");
    }

    [Fact]
    public async Task Handle_CollectionNotFound_ThrowsNotFoundException()
    {
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection>());

        var act = () => _sut.Handle(new DeleteCollectionCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CollectionBelongsToDifferentCustomer_ThrowsNotFoundException()
    {
        var collectionId = Guid.NewGuid();
        var collection = CreateCollection(Guid.NewGuid(), collectionId);
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });

        var act = () => _sut.Handle(new DeleteCollectionCommand(collectionId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidEmptyCollection_SoftDeletesCollection()
    {
        var collectionId = Guid.NewGuid();
        var collection = CreateCollection(CustomerId, collectionId);

        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.WardrobeCollectionItems).ReturnsDbSet(new List<WardrobeCollectionItem>());
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(new DeleteCollectionCommand(collectionId), CancellationToken.None);

        collection.IsDeleted.Should().BeTrue();
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CollectionWithItems_SoftDeletesCollectionAndAllItems()
    {
        var collectionId = Guid.NewGuid();
        var collection = CreateCollection(CustomerId, collectionId);
        var item1 = CreateItem(collectionId, Guid.NewGuid());
        var item2 = CreateItem(collectionId, Guid.NewGuid());

        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.WardrobeCollectionItems).ReturnsDbSet(new List<WardrobeCollectionItem> { item1, item2 });
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(new DeleteCollectionCommand(collectionId), CancellationToken.None);

        collection.IsDeleted.Should().BeTrue();
        item1.IsDeleted.Should().BeTrue();
        item2.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsSaveChangesOnce()
    {
        var collectionId = Guid.NewGuid();
        var collection = CreateCollection(CustomerId, collectionId);

        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.WardrobeCollectionItems).ReturnsDbSet(new List<WardrobeCollectionItem>());
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(new DeleteCollectionCommand(collectionId), CancellationToken.None);

        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}