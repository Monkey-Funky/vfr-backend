using Application.Features.Customer.Outfits.Commands.CreateOutfit;
using Application.Features.Customer.Outfits.DTOs;
using Application.Features.Customer.OutfitSuggestions.DTOs;
using Application.Features.Customer.OutfitSuggestions.Queries.GetOutfitSuggestions;
using Domain.Entities.Customer;
using Domain.Entities.Retailer;
using Domain.Enums.Customer;
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
        // Seed a real product owned by the test retailer.
        var productId = await SeedProductAsync("Outfit Product");

        // BUG FIX #2: CreateOutfitCommandHandler enforces a security rule — every product
        // in the outfit must already be in the customer's CustomerFavorites.
        // The original test only seeded the product but never favorited it, so the handler
        // threw BusinessRuleException("INVALID_OUTFIT_ITEMS") → 422 UnprocessableEntity.
        await SeedFavoriteAsync(productId);

        // BUG FIX #1: The original test sent an anonymous object with SlotType = "Top"
        // (a C# string), which PostAsJsonAsync serializes to the JSON string "Top".
        // The server's CreateOutfitItemDto expects SlotType (a C# enum), and because
        // JsonStringEnumConverter is NOT registered in Program.cs, the model binder
        // cannot convert the string "Top" to SlotType.Top → 400 Bad Request.
        //
        // Fix: use the strongly-typed CreateOutfitCommand / CreateOutfitItemDto with
        // the actual SlotType enum value — exactly as OutfitsControllerTests does.
        // PostAsJsonAsync then serializes SlotType.Top as the integer 0, which the
        // server deserializes correctly without any special converter.
        var command = new CreateOutfitCommand(
            Name: "AI Casual Look",
            StyleCategory: "Casual",
            Items: new List<CreateOutfitItemDto>
            {
                new(productId, SlotType.Top, DisplayOrder: 0)
            });

        var response = await CustomerClient.PostAsJsonAsync(
            "/api/customer/wardrobe/suggestions/save", command);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created);
    }

    // =========================================================================
    // Private Seed Helpers
    // =========================================================================

    /// <summary>
    /// Creates a Product row owned by <see cref="_retailerId"/> and returns its ID.
    /// </summary>
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

    /// <summary>
    /// Adds <paramref name="productId"/> to <see cref="_customerId"/>'s favorites.
    /// Required before saving an outfit — <c>CreateOutfitCommandHandler</c> rejects
    /// any product that has not been favorited first (INVALID_OUTFIT_ITEMS rule).
    /// </summary>
    private async Task SeedFavoriteAsync(Guid productId)
    {
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var favorite = CustomerFavorite.Create(_customerId, productId, _retailerId);
            db.CustomerFavorites.Add(favorite);
            await db.SaveChangesAsync();
        });
    }

    /// <summary>
    /// Creates a Product and immediately favorites it for the test customer.
    /// Used by suggestion-generation tests that need at least one wardrobe item.
    /// </summary>
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