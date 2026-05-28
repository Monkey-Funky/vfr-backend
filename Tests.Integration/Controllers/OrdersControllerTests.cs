using System.Net;
using System.Net.Http.Json;
using API.Controllers.Orders;
using Application.Features.Notifications.DTOs;
using Application.Features.Notifications.Queries.GetNotifications;
using Application.Features.Orders.DTOs;
using Domain.Entities.Notifications;
using Domain.Entities.Orders;
using Domain.Entities.Retailer;
using Domain.Entities.Subscriptions;
using Domain.Enums.Orders;
using Domain.Enums.Subscription;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class OrdersControllerTests : IntegrationTestBase
{
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
    private readonly Guid _customerId = Guid.Parse(TestAuthHandler.DefaultCustomerId);
    private static readonly Guid BasicMonthlyPlanId = new("11111111-1111-1111-1111-111111111001");

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

    [Fact]
    public async Task PlaceOrder_ThenCheckInventory_ShouldBeDecremented()
    {
        // BUG FIX: Must create a real Product first — inventory_records has a NOT-NULL FK
        // (fk_inventory_records_products_product_id) that requires the product to exist.
        var productId = await SeedProductAsync("Inventory Test Product");
        const int initialStock = 10;
        const int orderQuantity = 3;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var inventory = InventoryRecord.Create(_retailerId, productId, "Inventory Test Product", initialStock);
            db.InventoryRecords.Add(inventory);
            await db.SaveChangesAsync();
        });

        var orderId = await SeedOrderWithProductAsync(productId, orderQuantity);

        var processingResponse = await TransitionOrderStatusAsync(orderId, OrderStatus.Processing);
        processingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var shippedResponse = await TransitionOrderStatusAsync(orderId, OrderStatus.Shipped);
        shippedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var inventory = await db.InventoryRecords
                .AsNoTracking()
                .FirstAsync(ir => ir.ProductId == productId && ir.RetailerId == _retailerId);
            inventory.CurrentStock.Should().Be(initialStock - orderQuantity);
        });
    }

    [Fact]
    public async Task UpdateOrderStatus_ToDelivered_ThenCheckCommission_ShouldBeCreated()
    {
        await SeedActiveSubscriptionForRetailerAsync();
        var orderId = await SeedOrderAsync();

        await TransitionOrderToDeliveredAsync(orderId);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var commission = await db.CommissionRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.OrderId == orderId && c.RetailerId == _retailerId);

            commission.Should().NotBeNull();
            commission!.RetailerId.Should().Be(_retailerId);
            commission.OrderId.Should().Be(orderId);
            commission.CommissionRate.Should().BeGreaterThanOrEqualTo(0);
            commission.CommissionAmount.Should().Be(
                Math.Round(commission.OrderTotal * commission.CommissionRate, 2));
        });
    }

    [Fact]
    public async Task UpdateOrderStatus_ToDelivered_ThenCheckNotification_ShouldBeSentToCustomer()
    {
        var orderId = await SeedOrderAsync();

        await TransitionOrderToDeliveredAsync(orderId);

        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationsPagedResult>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Items.Should().NotBeEmpty();
        result.Data.Items.Should().Contain(n =>
            n.ResourceId == orderId &&
            n.Type == Notification.NotificationType.OrderStatusChanged);
    }

    [Fact]
    public async Task UpdateOrderStatus_ToDelivered_ThenCheckNotification_ShouldBeSentToRetailer()
    {
        var orderId = await SeedOrderAsync();

        await TransitionOrderToDeliveredAsync(orderId);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var notification = await db.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(n =>
                    n.RetailerId == _retailerId &&
                    n.ResourceId == orderId);

            notification.Should().NotBeNull();
            notification!.RetailerId.Should().Be(_retailerId);
            notification.ResourceId.Should().Be(orderId);
            notification.Type.Should().Be(Notification.NotificationType.OrderStatusChanged);
            notification.IsRead.Should().BeFalse();
        });
    }

    [Fact]
    public async Task UpdateOrderStatus_InvalidTransition_FromDeliveredToProcessing_ShouldReturn422()
    {
        var orderId = await SeedOrderAsync();
        await TransitionOrderToDeliveredAsync(orderId);

        var response = await TransitionOrderStatusAsync(orderId, OrderStatus.Processing);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId);
            order.Status.Should().Be(OrderStatus.Delivered);
        });
    }

    [Fact]
    public async Task PlaceOrder_WithOutOfStockItem_ShouldReturn422()
    {
        // BUG FIX: Must create a real Product first — inventory_records has a NOT-NULL FK
        // (fk_inventory_records_products_product_id) that requires the product to exist.
        var productId = await SeedProductAsync("Zero Stock Product");
        const int zeroStock = 0;
        const int orderQuantity = 1;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var inventory = InventoryRecord.Create(_retailerId, productId, "Zero Stock Product", zeroStock);
            db.InventoryRecords.Add(inventory);
            await db.SaveChangesAsync();
        });

        var orderId = await SeedOrderWithProductAsync(productId, orderQuantity);

        var processingResponse = await TransitionOrderStatusAsync(orderId, OrderStatus.Processing);
        processingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var shippedResponse = await TransitionOrderStatusAsync(orderId, OrderStatus.Shipped);
        shippedResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId);
            order.Status.Should().Be(OrderStatus.Processing);
        });
    }

    // =========================================================================
    // Private Seed Helpers
    // =========================================================================

    /// <summary>
    /// Creates a minimal Order with a NULL ProductId on its line item.
    /// ProductId is Guid? (nullable) by design — it is set to NULL when a product
    /// is deleted after the order was placed, so null is a valid test value and
    /// avoids the FK constraint (fk_order_items_products_product_id) that fires
    /// when a non-existent product GUID is used.
    /// </summary>
    private async Task<Guid> SeedOrderAsync()
    {
        Guid orderId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var items = new List<(Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity)>
            {
                // ProductId = null — valid per the domain model (product may be deleted);
                // avoids FK violation caused by referencing a non-existent product row.
                (null, "Test Product", 99.99m, 2)
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

    /// <summary>
    /// Creates an Order whose single line item references an existing product.
    /// The caller must ensure <paramref name="productId"/> already exists in the
    /// products table (use <see cref="SeedProductAsync"/> first).
    /// </summary>
    private async Task<Guid> SeedOrderWithProductAsync(Guid productId, int quantity)
    {
        Guid orderId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var items = new List<(Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity)>
            {
                (productId, "Test Product", 50.00m, quantity)
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

    /// <summary>
    /// Creates a real Product row in the products table owned by <see cref="_retailerId"/>
    /// and returns its generated ID.
    ///
    /// Required before inserting any InventoryRecord or OrderItem that references a
    /// product_id, because both tables have a NOT-NULL FK to products
    /// (fk_inventory_records_products_product_id / fk_order_items_products_product_id).
    /// </summary>
    /// <param name="name">
    /// Product display name. Must be unique per retailer within a test run
    /// (partial unique index: retailer_id + name WHERE is_deleted = false).
    /// Each caller should pass a distinct name to avoid conflicts.
    /// </param>
    private async Task<Guid> SeedProductAsync(string name = "Seeded Test Product")
    {
        Guid productId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(
                retailerId: _retailerId,
                name: name,
                price: 50.00m,
                status: "Active");

            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });
        return productId;
    }

    private async Task SeedActiveSubscriptionForRetailerAsync()
    {
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var alreadyExists = await db.Subscriptions
                .AnyAsync(s => s.RetailerId == _retailerId);

            if (alreadyExists)
                return;

            var subscription = Subscription.Create(
                _retailerId,
                BasicMonthlyPlanId,
                SubscriptionStatus.Active,
                DateTime.UtcNow,
                DateTime.UtcNow.AddMonths(1));

            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();
        });
    }

    private async Task<HttpResponseMessage> TransitionOrderStatusAsync(Guid orderId, string newStatus)
    {
        var request = new UpdateOrderStatusRequest(newStatus);
        return await Client.PatchAsJsonAsync(
            $"/api/retailers/{_retailerId}/orders/{orderId}/status", request);
    }

    private async Task TransitionOrderToDeliveredAsync(Guid orderId)
    {
        var processingResponse = await TransitionOrderStatusAsync(orderId, OrderStatus.Processing);
        processingResponse.EnsureSuccessStatusCode();

        var shippedResponse = await TransitionOrderStatusAsync(orderId, OrderStatus.Shipped);
        shippedResponse.EnsureSuccessStatusCode();

        var deliveredResponse = await TransitionOrderStatusAsync(orderId, OrderStatus.Delivered);
        deliveredResponse.EnsureSuccessStatusCode();
    }
}