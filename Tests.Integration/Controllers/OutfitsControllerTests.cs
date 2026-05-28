using System.Net;
using System.Net.Http.Json;
using Application.Features.Customer.Outfits.Commands.CreateOutfit;
using Application.Features.Customer.Outfits.DTOs;
using Domain.Entities.Customer;
using Domain.Entities.Retailer;
using Domain.Enums.Customer;
using Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class OutfitsControllerTests : IntegrationTestBase
{
    private readonly Guid _customerId = Guid.Parse(TestAuthHandler.DefaultCustomerId);
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

    public OutfitsControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetOutfits_ReturnsEmptyList_WhenNoOutfitsExist()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/outfits");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OutfitSummaryDto>>>();
        result!.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateOutfit_ReturnsCreated_WhenDataIsValid()
    {
        var productId = await SeedProductAsync();

        // The handler requires products to be favorited before creating an outfit
        await SeedFavoriteAsync(productId);

        var command = new CreateOutfitCommand(
            Name: "Summer Outfit",
            StyleCategory: "Casual",
            Items: new List<CreateOutfitItemDto>
            {
                new(productId, SlotType.Top, 0)
            });

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/outfits", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetOutfitById_ReturnsOutfit_WhenExists()
    {
        var outfitId = await SeedOutfitAsync("Test Outfit");

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/outfits/{outfitId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OutfitDetailDto>>();
        result!.Data!.Name.Should().Be("Test Outfit");
    }

    [Fact]
    public async Task GetOutfitById_ReturnsNotFound_WhenDoesNotExist()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/outfits/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteOutfit_ReturnsNoContent_WhenOutfitExists()
    {
        var outfitId = await SeedOutfitAsync("Deleteable Outfit");

        var response = await CustomerClient.DeleteAsync(
            $"/api/customers/{_customerId}/outfits/{outfitId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var outfit = await db.CustomerOutfits
                .IgnoreQueryFilters()
                .FirstAsync(o => o.Id == outfitId);
            outfit.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task GetOutfits_ReturnsForbidden_WhenCustomerIdDoesNotMatch()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{Guid.NewGuid()}/outfits");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> SeedOutfitAsync(string name)
    {
        Guid id = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var outfit = CustomerOutfit.Create(_customerId, name, "Casual");
            db.CustomerOutfits.Add(outfit);
            await db.SaveChangesAsync();
            id = outfit.Id;
        });
        return id;
    }

    private async Task<Guid> SeedProductAsync()
    {
        Guid productId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(_retailerId, "Outfit Product", price: 79.99m);
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
