using Application.Features.Customer.OutfitSuggestions.DTOs;
using Application.Features.Customer.OutfitSuggestions.Queries.GetOutfitSuggestions;
using Domain.Entities.Customer;
using Domain.Entities.Retailer;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;
[Collection(IntegrationTestCollection.Name)]
public sealed class OutfitSuggestionsControllerTests : IntegrationTestBase
{
    private readonly Guid _customerId = Guid.Parse(TestAuthHandler.DefaultCustomerId);
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

    public OutfitSuggestionsControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GenerateSuggestions_ReturnsOk_WhenCustomerHasNoWardrobe()
    {
        var query = new GetOutfitSuggestionsQuery(
            WeatherCondition: "Sunny",
            TemperatureF: 75m,
            Occasion: "Casual",
            Mood: null);

        var response = await CustomerClient.PostAsJsonAsync(
            "/api/customer/wardrobe/suggestions", query);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<OutfitSuggestionResultDto>>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateSuggestions_ReturnsOk_WhenCustomerHasWardrobeItems()
    {
        var productId = await SeedProductAndFavoriteAsync("Casual Top", "Tops");

        var query = new GetOutfitSuggestionsQuery(
            WeatherCondition: "Cloudy",
            TemperatureF: 65m,
            Occasion: "Work",
            Mood: "Professional");

        var response = await CustomerClient.PostAsJsonAsync(
            "/api/customer/wardrobe/suggestions", query);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<OutfitSuggestionResultDto>>>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateSuggestions_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var anonClient = Factory.CreateAnonymousClient();

        var query = new GetOutfitSuggestionsQuery(
            WeatherCondition: "Sunny",
            TemperatureF: 80m,
            Occasion: "Casual",
            Mood: null);

        var response = await anonClient.PostAsJsonAsync(
            "/api/customer/wardrobe/suggestions", query);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SaveSuggestion_ReturnsSuccess_WhenDataIsValid()
    {
        var productId = await SeedProductAsync("Outfit Product");

        var request = new
        {
            Name = "AI Casual Look",
            StyleCategory = "Casual",
            Items = new[]
            {
                new { ProductId = productId, SlotType = "Top", DisplayOrder = 0 }
            }
        };

        var response = await CustomerClient.PostAsJsonAsync(
            "/api/customer/wardrobe/suggestions/save", request);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created);
    }

    private async Task<Guid> SeedProductAsync(string name)
    {
        Guid productId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(_retailerId, name, price: 199m,
                status: Domain.Enums.Product.ProductStatus.Active);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });
        return productId;
    }

    private async Task<Guid> SeedProductAndFavoriteAsync(string productName, string categoryHint)
    {
        Guid productId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(_retailerId, productName, price: 149m,
                status: Domain.Enums.Product.ProductStatus.Active);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;

            var favorite = CustomerFavorite.Create(_customerId, product.Id, _retailerId);
            db.CustomerFavorites.Add(favorite);
            await db.SaveChangesAsync();
        });
        return productId;
    }
}