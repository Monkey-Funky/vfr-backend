using Application.Features.Customer.Wardrobe.Commands.RemoveItemFromCollection;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Moq.EntityFrameworkCore;

namespace Tests.Unit.Application.Features.Customer.Wardrobe;

public sealed class RemoveItemFromCollectionCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly RemoveItemFromCollectionCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid RetailerId = Guid.NewGuid();

    public RemoveItemFromCollectionCommandHandlerTests()
    {
        _sut = new RemoveItemFromCollectionCommandHandler(
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

    private static CustomerFavorite CreateFavorite(Guid customerId, Guid productId, Guid favoriteId)
    {
        var fav = CustomerFavorite.Create(customerId, productId, RetailerId);
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(fav, favoriteId);
        return fav;
    }

    private static WardrobeCollectionItem CreateCollectionItem(Guid collectionId, Guid favoriteId, Guid itemId)
    {
        var item = WardrobeCollectionItem.Create(collectionId, favoriteId);
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(item, itemId);
        return item;
    }

    [Fact]
    public async Task Handle_CustomerIdIsNull_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new RemoveItemFromCollectionCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*authenticated customers*");
    }

    [Fact]
    public async Task Handle_CollectionNotFoundOrNotOwned_ThrowsNotFoundException()
    {
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection>());
        _contextMock.Setup(x => x.WardrobeCollectionItems).ReturnsDbSet(new List<WardrobeCollectionItem>());
        _contextMock.Setup(x => x.CustomerFavorites).ReturnsDbSet(new List<CustomerFavorite>());

        var act = () => _sut.Handle(new RemoveItemFromCollectionCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CollectionBelongsToDifferentCustomer_ThrowsNotFoundException()
    {
        var collectionId = Guid.NewGuid();
        var collection = CreateCollection(Guid.NewGuid(), collectionId);

        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.WardrobeCollectionItems).ReturnsDbSet(new List<WardrobeCollectionItem>());
        _contextMock.Setup(x => x.CustomerFavorites).ReturnsDbSet(new List<CustomerFavorite>());

        var act = () => _sut.Handle(new RemoveItemFromCollectionCommand(collectionId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ProductNotInCollection_ThrowsNotFoundException()
    {
        var collectionId = Guid.NewGuid();
        var collection = CreateCollection(CustomerId, collectionId);

        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.WardrobeCollectionItems).ReturnsDbSet(new List<WardrobeCollectionItem>());
        _contextMock.Setup(x => x.CustomerFavorites).ReturnsDbSet(new List<CustomerFavorite>());

        var act = () => _sut.Handle(new RemoveItemFromCollectionCommand(collectionId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_SoftDeletesCollectionItem()
    {
        var collectionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var favoriteId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var collection = CreateCollection(CustomerId, collectionId);
        var favorite = CreateFavorite(CustomerId, productId, favoriteId);
        var item = CreateCollectionItem(collectionId, favoriteId, itemId);

        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.WardrobeCollectionItems).ReturnsDbSet(new List<WardrobeCollectionItem> { item });
        _contextMock.Setup(x => x.CustomerFavorites).ReturnsDbSet(new List<CustomerFavorite> { favorite });
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(new RemoveItemFromCollectionCommand(collectionId, productId), CancellationToken.None);

        item.IsDeleted.Should().BeTrue();
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}