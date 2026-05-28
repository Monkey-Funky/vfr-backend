using System.Net;
using System.Net.Http.Json;
using Application.Features.Customer.FitFeedback.Commands.SubmitFitFeedback;
using Application.Features.Customer.FitFeedback.DTOs;
using Domain.Entities.Customer;
using Domain.Entities.Orders;
using Domain.Entities.Retailer;
using Domain.Enums.Orders;
using Shared.DTOs;
using Tests.Integration.Fixtures;


namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class FitFeedbackControllerTests : IntegrationTestBase
{
    private readonly Guid _customerId = Guid.Parse(TestAuthHandler.DefaultCustomerId);
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

    public FitFeedbackControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task SubmitFitFeedback_ReturnsCreated_WhenOrderIsDelivered()
    {
        var (orderItemId, productId) = await SeedDeliveredOrderAsync();

        var command = new SubmitFitFeedbackCommand(
            OrderItemId: orderItemId,
            ProductId: productId,
            FitRating: 4,
            PredictedSize: "M",
            ActualSizeNeeded: "L",
            FeedbackNotes: "Runs a bit small",
            TryOnSessionId: null);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/fit-feedback", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<FitFeedbackDto>>();
        result!.Data.Should().NotBeNull();
        result.Data!.FitRating.Should().Be(4);
    }

    [Fact]
    public async Task SubmitFitFeedback_ReturnsNotFound_WhenOrderItemDoesNotExist()
    {
        var command = new SubmitFitFeedbackCommand(
            OrderItemId: Guid.NewGuid(),
            ProductId: Guid.NewGuid(),
            FitRating: 3,
            PredictedSize: null,
            ActualSizeNeeded: null,
            FeedbackNotes: null,
            TryOnSessionId: null);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/fit-feedback", command);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitFitFeedback_ReturnsConflict_WhenFeedbackAlreadySubmitted()
    {
        var (orderItemId, productId) = await SeedDeliveredOrderAsync();

        var command = new SubmitFitFeedbackCommand(
            OrderItemId: orderItemId,
            ProductId: productId,
            FitRating: 5,
            PredictedSize: null,
            ActualSizeNeeded: null,
            FeedbackNotes: null,
            TryOnSessionId: null);

        await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/fit-feedback", command);

        var response = await CustomerClient.PostAsJsonAsync(
            $"/api/customers/{_customerId}/fit-feedback", command);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetFitFeedbackByOrder_ReturnsEmptyList_WhenNoFeedbackExists()
    {
        // Seed a real order so the handler's ownership check passes
        var (orderItemId, _) = await SeedDeliveredOrderAsync();
        var orderId = await GetOrderIdFromItemAsync(orderItemId);

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/fit-feedback/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FitFeedbackDto>>>();
        result!.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFitFeedbackByProduct_ReturnsEmptyList_WhenNoFeedbackExists()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/fit-feedback/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FitFeedbackDto>>>();
        result!.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFitFeedbackByOrder_ReturnsFeedback_WhenFeedbackExists()
    {
        var (orderItemId, productId) = await SeedDeliveredOrderAsync();
        await SeedFitFeedbackAsync(orderItemId, productId);

        var orderId = await GetOrderIdFromItemAsync(orderItemId);

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/fit-feedback/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FitFeedbackDto>>>();
        result!.Data!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetFitFeedbackByProduct_ReturnsFeedback_WhenFeedbackExists()
    {
        var (orderItemId, productId) = await SeedDeliveredOrderAsync();
        await SeedFitFeedbackAsync(orderItemId, productId);

        var response = await CustomerClient.GetAsync(
            $"/api/customers/{_customerId}/fit-feedback/products/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<FitFeedbackDto>>>();
        result!.Data!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetFitFeedbackByOrder_ReturnsForbidden_WhenCustomerIdDoesNotMatch()
    {
        var response = await CustomerClient.GetAsync(
            $"/api/customers/{Guid.NewGuid()}/fit-feedback/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(Guid OrderItemId, Guid ProductId)> SeedDeliveredOrderAsync()
    {
        Guid orderItemId = Guid.Empty;
        Guid productId = Guid.NewGuid();

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(_retailerId, "Feedback Product", price: 199m,
                status: Domain.Enums.Product.ProductStatus.Active);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;

            var items = new List<(Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity)>
        {
            (product.Id, product.Name, product.Price ?? 0m, 1)  
        };

            var order = Order.Create(_retailerId, _customerId, "Test Customer", items);
            order.UpdateStatus(OrderStatus.Processing);
            order.UpdateStatus(OrderStatus.Shipped);
            order.UpdateStatus(OrderStatus.Delivered);

            db.Orders.Add(order);
            await db.SaveChangesAsync();

            orderItemId = order.Items[0].Id;
        });

        return (orderItemId, productId);
    }

    private async Task SeedFitFeedbackAsync(Guid orderItemId, Guid productId)
    {
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var feedback = FitFeedback.Create(
                customerId: _customerId,
                orderItemId: orderItemId,
                productId: productId,
                fitRating: 4,
                predictedSize: "M",
                actualSizeNeeded: "L");

            db.FitFeedback.Add(feedback);
            await db.SaveChangesAsync();
        });
    }

    private async Task<Guid> GetOrderIdFromItemAsync(Guid orderItemId)
    {
        Guid orderId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var item = await db.Set<OrderItem>().FindAsync(orderItemId);
            orderId = item!.OrderId;
        });
        return orderId;
    }
}