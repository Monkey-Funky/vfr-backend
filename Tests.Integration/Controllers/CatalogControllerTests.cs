using System.Net;
using System.Net.Http.Json;
using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Catalog.Queries.CompareProducts;
using Domain.Entities.Retailer;
using Domain.Enums.Product;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class CatalogControllerTests : IntegrationTestBase
{
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

    public CatalogControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task BrowseProducts_ReturnsOk_WhenCalledAnonymously()
    {
        var anonymousClient = Factory.CreateAnonymousClient();

        var response = await anonymousClient.GetAsync("/api/catalog/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProductCardDto>>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task BrowseProducts_ReturnsProducts_WhenProductsExist()
    {
        await SeedActiveProductAsync("Catalog Product 1");
        await SeedActiveProductAsync("Catalog Product 2");

        var anonymousClient = Factory.CreateAnonymousClient();
        var response = await anonymousClient.GetAsync("/api/catalog/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProductCardDto>>>();
        result!.Data!.Items.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetProductDetail_ReturnsOk_WhenProductExists()
    {
        var productId = await SeedActiveProductAsync("Detail Product");

        var anonymousClient = Factory.CreateAnonymousClient();
        var response = await anonymousClient.GetAsync($"/api/catalog/products/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Application.Features.Customer.Catalog.DTOs.ProductDetailDto>>();
        result!.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProductDetail_ReturnsNotFound_WhenProductDoesNotExist()
    {
        var anonymousClient = Factory.CreateAnonymousClient();

        var response = await anonymousClient.GetAsync($"/api/catalog/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSimilarProducts_ReturnsOk_WhenProductExists()
    {
        var productId = await SeedActiveProductAsync("Similar Source");

        var anonymousClient = Factory.CreateAnonymousClient();
        var response = await anonymousClient.GetAsync($"/api/catalog/products/{productId}/similar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BrowseCategories_ReturnsOk()
    {
        var anonymousClient = Factory.CreateAnonymousClient();

        var response = await anonymousClient.GetAsync("/api/catalog/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BrowseOffers_ReturnsOk()
    {
        var anonymousClient = Factory.CreateAnonymousClient();

        var response = await anonymousClient.GetAsync("/api/catalog/offers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CompareProducts_ReturnsOk_WhenProductIdsAreValid()
    {
        var productId1 = await SeedActiveProductAsync("Compare A");
        var productId2 = await SeedActiveProductAsync("Compare B");

        var anonymousClient = Factory.CreateAnonymousClient();
        var query = new CompareProductsQuery(new[] { productId1, productId2 });

        var response = await anonymousClient.PostAsJsonAsync("/api/catalog/products/compare", query);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<Guid> SeedActiveProductAsync(string name)
    {
        Guid productId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(_retailerId, name, price: 99.99m, status: ProductStatus.Active);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });
        return productId;
    }
}
