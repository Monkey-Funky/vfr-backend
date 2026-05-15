using System.Net;
using System.Net.Http.Json;
using API.Controllers.Orders;
using Application.Features.Orders.DTOs;
using Domain.Entities.Orders;
using Domain.Enums.Orders;
using Shared.DTOs;
using Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class OrdersControllerTests : IntegrationTestBase
{
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
    private readonly Guid _customerId = Guid.Parse(TestAuthHandler.DefaultCustomerId);

    public OrdersControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetOrders_ReturnsEmptyList_WhenNoOrdersExist()
    {
        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OrderDto>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrders_ReturnsOrders_WhenOrdersExist()
    {
        await SeedOrderAsync();

        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OrderDto>>>();
        result!.Data!.Items.Should().NotBeEmpty();
        result.Data.TotalCount.Should().BeGreaterThan(0);
    }

    

    [Fact]
    public async Task GetOrderById_ReturnsNotFound_WhenOrderDoesNotExist()
    {
        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateOrderStatus_TransitionsCorrectly_FromNotProcessedToProcessing()
    {
        var orderId = await SeedOrderAsync();
        var request = new UpdateOrderStatusRequest(OrderStatus.Processing);

        var response = await Client.PatchAsJsonAsync(
            $"/api/retailers/{_retailerId}/orders/{orderId}/status", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var order = await db.Orders.FirstAsync(o => o.Id == orderId);
            order.Status.Should().Be(OrderStatus.Processing);
        });
    }

    [Fact]
    public async Task UpdateOrderStatus_ReturnsError_ForInvalidTransition()
    {
        var orderId = await SeedOrderAsync();
        var request = new UpdateOrderStatusRequest(OrderStatus.Delivered);

        var response = await Client.PatchAsJsonAsync(
            $"/api/retailers/{_retailerId}/orders/{orderId}/status", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetOrders_ReturnsForbidden_WhenRetailerIdDoesNotMatch()
    {
        var otherRetailerId = Guid.NewGuid();

        var response = await Client.GetAsync($"/api/retailers/{otherRetailerId}/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExportCsv_ReturnsCsvContent_WhenAuthenticated()
    {
        await SeedOrderAsync();

        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/orders/export/csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
    }

    private async Task<Guid> SeedOrderAsync()
    {
        Guid orderId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var items = new List<(Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity)>
            {
                (Guid.NewGuid(), "Test Product", 99.99m, 2)
            };

            var order = Order.Create(
                _retailerId,
                _customerId,
                "Test Customer",
                items);

            db.Orders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        });
        return orderId;
    }
}
