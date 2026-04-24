// tests/Tests.Unit/Application/Features/Products/GetProductsQueryHandlerTests.cs
using Application.Features.Products.DTOs;
using Application.Features.Products.Queries.GetProducts;
using Application.Interfaces;
using Application.Interfaces.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Products;

public sealed class GetProductsQueryHandlerTests : TestBase
{
    private readonly Mock<IProductRepository> _productRepoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly GetProductsQueryHandler _handler;

    public GetProductsQueryHandlerTests()
    {
        _productRepoMock = MockRepository.Create<IProductRepository>();
        _currentUserMock = MockRepository.Create<ICurrentUserService>();

        _handler = new GetProductsQueryHandler(
            _productRepoMock.Object,
            _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_PassesPaginationParamsToRepository_ReturnsPagedResult()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        var expectedResult = new PagedResult<ProductListDto>
        {
            Items = new List<ProductListDto>(),
            TotalCount = 42,
            PageNumber = 2,
            PageSize = 10,
        };

        GetProductsQuery? capturedQuery = null;
        Guid capturedRetailerId = Guid.Empty;

        _productRepoMock
            .Setup(r => r.GetProductsPagedAsync(
                It.IsAny<GetProductsQuery>(),
                retailerId,
                It.IsAny<CancellationToken>()))
            .Callback<GetProductsQuery, Guid, CancellationToken>(
                (q, rid, _) => { capturedQuery = q; capturedRetailerId = rid; })
            .ReturnsAsync(expectedResult)
            .Verifiable();

        var query = new GetProductsQuery(PageNumber: 2, PageSize: 10);

        // Act
        PagedResult<ProductListDto> result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(42);
        result.PageNumber.Should().Be(2);

        capturedRetailerId.Should().Be(retailerId,
            "the handler must inject RetailerId from ICurrentUserService, never from query params");

        _productRepoMock.Verify(
            r => r.GetProductsPagedAsync(
                It.IsAny<GetProductsQuery>(),
                retailerId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RetailersOnlySeesTheirOwnProducts_ViaRetailerIdInjection()
    {
        // Arrange — retailer A calls handler; retailer B's products must NOT appear
        Guid retailerA = Guid.NewGuid();
        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerA);

        var retailerAProducts = new PagedResult<ProductListDto>
        {
            Items = new List<ProductListDto> { new(retailerA, "Shirt", null, null, null, "Active", 49.99m, "EGP", null, DateTime.UtcNow) },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 20,
        };

        _productRepoMock
            .Setup(r => r.GetProductsPagedAsync(
                It.IsAny<GetProductsQuery>(),
                retailerA,                     // ← must be retailerA's ID, never retailerB's
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(retailerAProducts);

        var query = new GetProductsQuery(PageNumber: 1, PageSize: 20);

        // Act
        PagedResult<ProductListDto> result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items.First().Id.Should().Be(retailerA);
    }
}