using System.Net;
using System.Net.Http.Json;
using API.Controllers.Customer;
using Application.Features.Customer.Wardrobe.Commands.CreateCollection;
using Application.Features.Customer.Wardrobe.DTOs;
using Domain.Entities.Customer;
using Domain.Entities.Retailer;
using Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Domain.Enums.Product;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class WardrobeControllerTests : IntegrationTestBase
{
    private readonly Guid _customerId = Guid.Parse(TestAuthHandler.DefaultCustomerId);
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

    public WardrobeControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetCollections_ReturnsEmptyList_WhenNoCollectionsExist()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/wardrobe/collections");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<WardrobeCollectionDto>>>();
        result!.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateCollection_ReturnsCreated_WhenDataIsValid()
    {
        var command = new CreateCollectionCommand("My Summer Collection");

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/wardrobe/collections", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetCollections_ReturnsCollections_WhenSeeded()
    {
        await SeedCollectionAsync("Winter Styles");

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/wardrobe/collections");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<WardrobeCollectionDto>>>();
        result!.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RenameCollection_ReturnsNoContent_WhenCollectionExists()
    {
        var collectionId = await SeedCollectionAsync("Old Name");
        var request = new RenameCollectionRequestDto("New Name");

        var response = await CustomerClient.PatchAsJsonAsync(
            $"/api/customers/{_customerId}/wardrobe/collections/{collectionId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var collection = await db.WardrobeCollections.FirstAsync(c => c.Id == collectionId);
            collection.Name.Should().Be("New Name");
        });
    }

    [Fact]
    public async Task DeleteCollection_ReturnsNoContent_WhenCollectionExists()
    {
        var collectionId = await SeedCollectionAsync("Deleteable");

        var response = await CustomerClient.DeleteAsync(
            $"/api/customers/{_customerId}/wardrobe/collections/{collectionId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var collection = await db.WardrobeCollections
                .IgnoreQueryFilters()
                .FirstAsync(c => c.Id == collectionId);
            collection.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task AddItemToCollection_ReturnsNoContent_WhenDataIsValid()
    {
        var collectionId = await SeedCollectionAsync("Items Collection");
        var productId = await SeedProductAsync();

        await SeedFavoriteAsync(productId);

        var request = new AddItemToCollectionRequestDto(productId);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/wardrobe/collections/{collectionId}/items", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetCollections_ReturnsForbidden_WhenCustomerIdDoesNotMatch()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{Guid.NewGuid()}/wardrobe/collections");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> SeedCollectionAsync(string name)
    {
        Guid id = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var collection = WardrobeCollection.Create(_customerId, name);
            db.WardrobeCollections.Add(collection);
            await db.SaveChangesAsync();
            id = collection.Id;
        });
        return id;
    }

    private async Task<Guid> SeedProductAsync()
    {
        Guid productId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(_retailerId, "Wardrobe Test Product", price: 149.99m,
                status: ProductStatus.Active);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });
        return productId;
    }

    private async Task SeedFavoriteAsync(Guid productId)
    {
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var favorite = CustomerFavorite.Create(_customerId, productId, _retailerId);
            db.CustomerFavorites.Add(favorite);
            await db.SaveChangesAsync();
        });
    }
}
