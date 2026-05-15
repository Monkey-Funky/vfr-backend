using System.Net;
using System.Net.Http.Json;
using API.Controllers.Customer;
using Application.Features.Customer.Catalog.DTOs;
using Domain.Entities.Customer;
using Domain.Entities.Retailer;
using Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class FavoritesControllerTests : IntegrationTestBase
{
    private readonly Guid _customerId = Guid.Parse(TestAuthHandler.DefaultCustomerId);
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

    public FavoritesControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetFavorites_ReturnsEmptyList_WhenNoFavoritesExist()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/favorites");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProductCardDto>>>();
        result!.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ToggleFavorite_ReturnsOk_WhenProductExists()
    {
        var productId = await SeedProductAsync();
        var request = new ToggleFavoriteRequestDto(productId);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/favorites/toggle", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ToggleFavoriteResponseDto>>();
        result!.Data!.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleFavorite_RemovesFavorite_WhenAlreadyFavorited()
    {
        var productId = await SeedProductAsync();
        var request = new ToggleFavoriteRequestDto(productId);

        await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/favorites/toggle", request);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/favorites/toggle", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ToggleFavoriteResponseDto>>();
        result!.Data!.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public async Task CheckFavorites_ReturnsStatus_WhenProductIdsProvided()
    {
        var productId = await SeedProductAsync();

        await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/favorites/toggle",
            new ToggleFavoriteRequestDto(productId));

        var request = new CheckFavoritesRequestDto(new[] { productId });
        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/favorites/check", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Dictionary<Guid, bool>>>();
        result!.Data!.Should().ContainKey(productId);
        result.Data[productId].Should().BeTrue();
    }

    [Fact]
    public async Task GetFavorites_ReturnsForbidden_WhenCustomerIdDoesNotMatch()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{Guid.NewGuid()}/favorites");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> SeedProductAsync()
    {
        Guid productId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(_retailerId, "Favorite Test Product", price: 199.99m);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });
        return productId;
    }
}
