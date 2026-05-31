using Application.Features.Products.DTOs;
using Application.Features.Products.Queries.GetProducts;
using Application.Interfaces.Services;
using Shared.DTOs;

namespace Tests.Unit.ApplicationTests.Features.Products;

public sealed class GetProductsQueryHandlerTests
{
    private readonly Mock<IProductRepository> _productRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly GetProductsQueryHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();

    public GetProductsQueryHandlerTests()
    {
        _sut = new GetProductsQueryHandler(_productRepoMock.Object, _currentUserServiceMock.Object, _cacheServiceMock.Object);
        // Cache miss for all GetAsync calls — Moq returns Task<T?> default (null)
        // which simulates a cache miss so the handler always exercises the DB path.
        // RemoveAsync / RemoveByPrefixAsync are stubbed to complete successfully.
        _cacheServiceMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock
            .Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock
            .Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
    }

    private void SetupPagedResult(PagedResult<ProductListDto> result) =>
        _productRepoMock
            .Setup(x => x.GetProductsPagedAsync(
                It.IsAny<GetProductsQuery>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private static PagedResult<ProductListDto> EmptyResult() => new()
    {
        Items = [],
        TotalCount = 0,
        PageNumber = 1,
        PageSize = 20
    };

    private static PagedResult<ProductListDto> ResultWithItems(int count, int pageNumber = 1, int pageSize = 20) => new()
    {
        Items = Enumerable.Range(0, count)
            .Select(i => new ProductListDto(
                Guid.NewGuid(), $"Product {i}", "Fashion", null, null,
                "Active", 100m, "EGP", null, DateTime.UtcNow))
            .ToList()
            .AsReadOnly(),
        TotalCount = count,
        PageNumber = pageNumber,
        PageSize = pageSize
    };

    [Fact]
    public async Task Handle_NoProducts_ReturnsEmptyPagedResult()
    {
        SetupPagedResult(EmptyResult());

        var result = await _sut.Handle(new GetProductsQuery(), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ProductsExist_ReturnsPagedList()
    {
        SetupPagedResult(ResultWithItems(5));

        var result = await _sut.Handle(new GetProductsQuery(), CancellationToken.None);

        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_FilterByCategory_ReturnsCorrectProducts()
    {
        var categoryId = Guid.NewGuid();
        SetupPagedResult(ResultWithItems(2));

        var result = await _sut.Handle(new GetProductsQuery(CategoryId: categoryId), CancellationToken.None);

        _productRepoMock.Verify(x => x.GetProductsPagedAsync(
            It.Is<GetProductsQuery>(q => q.CategoryId == categoryId),
            RetailerId,
            It.IsAny<CancellationToken>()), Times.Once);

        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FilterByStatus_ReturnsCorrectProducts()
    {
        SetupPagedResult(ResultWithItems(3));

        var result = await _sut.Handle(new GetProductsQuery(Status: "Active"), CancellationToken.None);

        _productRepoMock.Verify(x => x.GetProductsPagedAsync(
            It.Is<GetProductsQuery>(q => q.Status == "Active"),
            RetailerId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SearchByName_ReturnsMatchingProducts()
    {
        SetupPagedResult(ResultWithItems(1));

        await _sut.Handle(new GetProductsQuery(SearchTerm: "Blue Shirt"), CancellationToken.None);

        _productRepoMock.Verify(x => x.GetProductsPagedAsync(
            It.Is<GetProductsQuery>(q => q.SearchTerm == "Blue Shirt"),
            RetailerId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RetailerSeesOnlyOwnProducts()
    {
        SetupPagedResult(EmptyResult());

        await _sut.Handle(new GetProductsQuery(), CancellationToken.None);

        _productRepoMock.Verify(x => x.GetProductsPagedAsync(
            It.IsAny<GetProductsQuery>(),
            RetailerId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new GetProductsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_PaginationParameters_PassedToRepository()
    {
        SetupPagedResult(ResultWithItems(5, pageNumber: 2, pageSize: 5));

        var result = await _sut.Handle(new GetProductsQuery(PageNumber: 2, PageSize: 5), CancellationToken.None);

        _productRepoMock.Verify(x => x.GetProductsPagedAsync(
            It.Is<GetProductsQuery>(q => q.PageNumber == 2 && q.PageSize == 5),
            RetailerId,
            It.IsAny<CancellationToken>()), Times.Once);

        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(5);
    }
}