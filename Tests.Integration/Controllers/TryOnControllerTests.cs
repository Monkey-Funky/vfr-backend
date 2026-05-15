using Application.Features.Customer.VirtualTryOn.Commands.InitiateTryOn;
using Application.Features.Customer.VirtualTryOn.DTOs;
using Domain.Entities.Customer;
using Domain.Entities.Retailer;
using Domain.Enums.Customer;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;
[Collection(IntegrationTestCollection.Name)]
public sealed class TryOnControllerTests : IntegrationTestBase
{
    private readonly Guid _customerId = Guid.Parse(TestAuthHandler.DefaultCustomerId);
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

    public TryOnControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetTryOnSessions_ReturnsEmptyList_WhenNoSessionsExist()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/try-on/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<VirtualTryOnSessionDto>>>();
        result!.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTryOnSessions_ReturnsSessions_WhenSessionsExist()
    {
        var productId = await SeedProductAsync();
        await SeedTryOnSessionAsync(productId);

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/try-on/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<VirtualTryOnSessionDto>>>();
        result!.Data!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTryOnSession_ReturnsNotFound_WhenSessionDoesNotExist()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/try-on/sessions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTryOnSession_ReturnsSession_WhenExists()
    {
        var productId = await SeedProductAsync();
        var sessionId = await SeedTryOnSessionAsync(productId);

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/try-on/sessions/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<VirtualTryOnSessionDto>>();
        result!.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(sessionId);
    }

    [Fact]
    public async Task GetTryOnSessionsByProduct_ReturnsSessions_WhenSessionsExist()
    {
        var productId = await SeedProductAsync();
        await SeedTryOnSessionAsync(productId);

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/products/{productId}/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<VirtualTryOnSessionDto>>>();
        result!.Data!.Items.Should().NotBeEmpty();
        result.Data.Items[0].ProductId.Should().Be(productId);
    }

    [Fact]
    public async Task GetTryOnSessionsByProduct_ReturnsEmptyList_WhenNoSessions()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/products/{Guid.NewGuid()}/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<VirtualTryOnSessionDto>>>();
        result!.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task InitiateTryOn_ReturnsNotFound_WhenProductDoesNotExist()
    {
        var command = new InitiateTryOnCommand(
            ProductId: Guid.NewGuid(),
            SessionType: TryOnSessionType.Overlay2D,
            AvatarId: null);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/try-on", command);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTryOnSessions_ReturnsForbidden_WhenCustomerIdDoesNotMatch()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{Guid.NewGuid()}/try-on/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> SeedProductAsync()
    {
        Guid productId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(_retailerId, "TryOn Product", price: 299m,
                status: Domain.Enums.Product.ProductStatus.Active);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });
        return productId;
    }

    private async Task<Guid> SeedTryOnSessionAsync(Guid productId)
    {
        Guid sessionId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var session = VirtualTryOnSession.Create(
                customerId: _customerId,
                productId: productId,
                retailerId: _retailerId,
                sessionType: TryOnSessionType.Overlay2D);

            db.VirtualTryOnSessions.Add(session);
            await db.SaveChangesAsync();
            sessionId = session.Id;
        });
        return sessionId;
    }
}
