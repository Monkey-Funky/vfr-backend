using Application.Features.Customer.Wardrobe.Commands.AddItemToCollection;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Moq.EntityFrameworkCore;

namespace Tests.Unit.Application.Features.Customer.Wardrobe;

public sealed class AddItemToCollectionCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly AddItemToCollectionCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid RetailerId = Guid.NewGuid();

    public AddItemToCollectionCommandHandlerTests()
    {
        _sut = new AddItemToCollectionCommandHandler(
            _contextMock.Object,
            _currentUserServiceMock.Object,
            _cacheServiceMock.Object);

        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
    }

    private static WardrobeCollection CreateCollection(Guid customerId, Guid? id = null)
    {
        var col = WardrobeCollection.Create(customerId, "My Collection");
        if (id.HasValue)
            typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(col, id.Value);
        return col;
    }

    private static Domain.Entities.Retailer.Product CreateActiveProduct(Guid productId)
    {
        var product = Domain.Entities.Retailer.Product.Create(RetailerId, "Shirt", null, null, null, null, "EGP", null, ProductStatus.Active);
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(product, productId);
        return product;
    }

    [Fact]
    public async Task Handle_ValidRequest_AddsItemToCollection()
    {
        var collectionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var collection = CreateCollection(CustomerId, collectionId);
        var product = CreateActiveProduct(productId);

        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.CustomerFavorites).ReturnsDbSet(new List<CustomerFavorite>());
        _contextMock.Setup(x => x.WardrobeCollectionItems).ReturnsDbSet(new List<WardrobeCollectionItem>());
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new AddItemToCollectionCommand(collectionId, productId);
        await _sut.Handle(command, CancellationToken.None);

        _contextMock.Verify(x => x.WardrobeCollectionItems.Add(It.IsAny<WardrobeCollectionItem>()), Times.Once);
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CollectionNotFound_ThrowsNotFoundException()
    {
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection>());

        var command = new AddItemToCollectionCommand(Guid.NewGuid(), Guid.NewGuid());
        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CollectionBelongsToAnotherCustomer_ThrowsUnauthorizedAccessException()
    {
        var collectionId = Guid.NewGuid();
        var collection = CreateCollection(Guid.NewGuid(), collectionId);
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });

        var command = new AddItemToCollectionCommand(collectionId, Guid.NewGuid());
        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not authorized*");
    }

    [Fact]
    public async Task Handle_ProductNotFound_ThrowsNotFoundException()
    {
        var collectionId = Guid.NewGuid();
        var collection = CreateCollection(CustomerId, collectionId);
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product>());

        var command = new AddItemToCollectionCommand(collectionId, Guid.NewGuid());
        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ProductNotActive_ThrowsNotFoundException()
    {
        var collectionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var collection = CreateCollection(CustomerId, collectionId);
        var product = Domain.Entities.Retailer.Product.Create(RetailerId, "Shirt", null, null, null, null, "EGP", null, ProductStatus.Draft);
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(product, productId);

        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });

        var command = new AddItemToCollectionCommand(collectionId, productId);
        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_FavoriteAlreadySoftDeleted_RestoresFavoriteAndAddsItem()
    {
        var collectionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var favoriteId = Guid.NewGuid();
        var collection = CreateCollection(CustomerId, collectionId);
        var product = CreateActiveProduct(productId);

        var deletedFavorite = CustomerFavorite.Create(CustomerId, productId, RetailerId);
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(deletedFavorite, favoriteId);
        deletedFavorite.SoftDelete();

        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.CustomerFavorites).ReturnsDbSet(new List<CustomerFavorite> { deletedFavorite });
        _contextMock.Setup(x => x.WardrobeCollectionItems).ReturnsDbSet(new List<WardrobeCollectionItem>());
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new AddItemToCollectionCommand(collectionId, productId);
        await _sut.Handle(command, CancellationToken.None);

        deletedFavorite.IsDeleted.Should().BeFalse();
        _contextMock.Verify(x => x.WardrobeCollectionItems.Add(It.IsAny<WardrobeCollectionItem>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FavoriteDoesNotExist_CreatesFavoriteAndAddsItem()
    {
        var collectionId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var collection = CreateCollection(CustomerId, collectionId);
        var product = CreateActiveProduct(productId);

        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.Products).ReturnsDbSet(new List<Domain.Entities.Retailer.Product> { product });
        _contextMock.Setup(x => x.CustomerFavorites).ReturnsDbSet(new List<CustomerFavorite>());
        _contextMock.Setup(x => x.WardrobeCollectionItems).ReturnsDbSet(new List<WardrobeCollectionItem>());
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(new AddItemToCollectionCommand(collectionId, productId), CancellationToken.None);

        _contextMock.Verify(x => x.CustomerFavorites.Add(It.IsAny<CustomerFavorite>()), Times.Once);
    }
}