using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Favorites.Queries.GetFavorites;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Domain.Enums.Product;
using Shared.DTOs;

namespace Tests.Unit.Application.Features.Customer.Favorites;

public sealed class GetFavoritesQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly GetFavoritesQueryHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OtherCustomerId = Guid.NewGuid();
    private static readonly Guid RetailerId = Guid.NewGuid();

    public GetFavoritesQueryHandlerTests()
    {
        _sut = new GetFavoritesQueryHandler(
            _contextMock.Object,
            _userServiceMock.Object);

        _userServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
    }

    private static CustomerFavorite BuildFavorite(Guid? customerId = null, Guid? productId = null)
        => CustomerFavorite.Create(
            customerId ?? CustomerId,
            productId ?? Guid.NewGuid(),
            RetailerId);

    private static Product BuildActiveProduct(Guid? id = null)
    {
        var product = Product.Create(RetailerId, $"Product-{Guid.NewGuid():N}", price: 99.99m, status: ProductStatus.Active);
        if (id.HasValue)
        {
            typeof(Domain.Common.BaseEntity)
                .GetProperty(nameof(Domain.Common.BaseEntity.Id))!
                .SetValue(product, id.Value);
        }
        return product;
    }

    private void SetupDbSets(
        List<CustomerFavorite> favorites,
        List<Product>? products = null,
        List<Offer>? offers = null)
    {
        var favoritesMock = favorites.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.CustomerFavorites).Returns(favoritesMock.Object);

        var productList = products ?? [];
        var productsMock = productList.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.Products).Returns(productsMock.Object);

        var offerList = offers ?? [];
        var offersMock = offerList.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.Offers).Returns(offersMock.Object);
    }

    [Fact]
    public async Task Handle_NoFavorites_ReturnsEmptyPagedResult()
    {
        SetupDbSets([]);

        var result = await _sut.Handle(new GetFavoritesQuery(1, 20), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_FavoritesExist_ReturnsMappedList()
    {
        var productId = Guid.NewGuid();
        var product = BuildActiveProduct(productId);
        var favorite = BuildFavorite(productId: productId);
        SetupDbSets([favorite], [product]);

        var result = await _sut.Handle(new GetFavoritesQuery(1, 20), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].IsFavorite.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CustomerSeesOnlyOwnFavorites()
    {
        var ownFavorite = BuildFavorite(customerId: CustomerId);
        var otherFavorite = BuildFavorite(customerId: OtherCustomerId);
        SetupDbSets([ownFavorite, otherFavorite]);

        var result = await _sut.Handle(new GetFavoritesQuery(1, 20), CancellationToken.None);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NullCustomerId_ThrowsUnauthorizedAccessException()
    {
        _userServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new GetFavoritesQuery(1, 20), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_InactiveFavoritedProducts_AreExcluded()
    {
        var productId = Guid.NewGuid();
        var inactiveProduct = Product.Create(RetailerId, "Inactive Product", price: 50m, status: ProductStatus.Draft);
        typeof(Domain.Common.BaseEntity)
            .GetProperty(nameof(Domain.Common.BaseEntity.Id))!
            .SetValue(inactiveProduct, productId);

        var favorite = BuildFavorite(productId: productId);
        SetupDbSets([favorite], [inactiveProduct]);

        var result = await _sut.Handle(new GetFavoritesQuery(1, 20), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().BeEmpty();
    }
}