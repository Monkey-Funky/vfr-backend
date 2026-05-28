using System.Net;
using System.Net.Http.Json;
using API.Controllers.Inventory;
using Application.Features.Inventory.DTOs;
using Domain.Entities.Notifications;
using Domain.Entities.Retailer;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class InventoryControllerTests : IntegrationTestBase
{
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

    public InventoryControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetInventory_ReturnsPaginatedList_WhenRecordsExist()
    {
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(_retailerId, "Product A", price: 10m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            db.InventoryRecords.Add(InventoryRecord.Create(_retailerId, p.Id, p.Name, 100));
            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/inventory");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<InventoryDto>>>();
        result!.Data!.Items.Should().NotBeEmpty();
        result.Data.Items[0].CurrentStock.Should().Be(100);
    }

    [Fact]
    public async Task AdjustStock_UpdatesQuantity_WhenRequestIsValid()
    {
        Guid inventoryId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(_retailerId, "Product B", price: 20m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            var inv = InventoryRecord.Create(_retailerId, p.Id, p.Name, 50);
            db.InventoryRecords.Add(inv);
            await db.SaveChangesAsync();
            inventoryId = inv.Id;
        });

        var request = new AdjustStockRequest(
            NewQuantity: 75,
            Type: AdjustmentType.ManualIncrease,
            Reason: "Stock delivery");

        var response = await Client.PatchAsJsonAsync(
            $"/api/retailers/{_retailerId}/inventory/{inventoryId}/adjust", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var inv = await db.InventoryRecords.FindAsync(inventoryId);
            inv!.CurrentStock.Should().Be(75);
        });
    }

    [Fact]
    public async Task SetLowStockThreshold_UpdatesThreshold_WhenRequestIsValid()
    {
        Guid inventoryId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(_retailerId, "Product C", price: 30m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            var inv = InventoryRecord.Create(_retailerId, p.Id, p.Name, 50, lowStockThreshold: 10);
            db.InventoryRecords.Add(inv);
            await db.SaveChangesAsync();
            inventoryId = inv.Id;
        });

        var request = new SetLowStockThresholdRequest(25);

        var response = await Client.PutAsJsonAsync(
            $"/api/retailers/{_retailerId}/inventory/{inventoryId}/threshold", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var inv = await db.InventoryRecords.FindAsync(inventoryId);
            inv!.LowStockThreshold.Should().Be(25);
        });
    }

    [Fact]
    public async Task ExportCsv_ReturnsFile_WhenCalled()
    {
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(_retailerId, "Export Product", price: 10m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            db.InventoryRecords.Add(InventoryRecord.Create(_retailerId, p.Id, p.Name, 10));
            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/inventory/export/csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Export Product");
    }

    [Fact]
    public async Task GetInventoryByProductId_ReturnsDetail_WhenProductExists()
    {
        Guid productId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(_retailerId, "Detail Product", price: 50m);
            db.Products.Add(p);
            await db.SaveChangesAsync();
            productId = p.Id;

            db.InventoryRecords.Add(InventoryRecord.Create(_retailerId, p.Id, p.Name, 30, lowStockThreshold: 5));
            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/inventory/product/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<InventoryDetailDto>>();
        result!.Data.Should().NotBeNull();
        result.Data!.CurrentStock.Should().Be(30);
    }

    [Fact]
    public async Task GetInventoryByProductId_ReturnsNotFound_WhenProductDoesNotExist()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/inventory/product/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteInventoryRecord_SoftDeletes_WhenRecordExists()
    {
        Guid inventoryId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(_retailerId, "Delete Inventory Product", price: 25m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            var inv = InventoryRecord.Create(_retailerId, p.Id, p.Name, 20);
            db.InventoryRecords.Add(inv);
            await db.SaveChangesAsync();
            inventoryId = inv.Id;
        });

        var response = await Client.DeleteAsync(
            $"/api/retailers/{_retailerId}/inventory/{inventoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var inv = await db.InventoryRecords
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == inventoryId);
            inv!.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task GetInventory_ReturnsEmptyList_WhenNoProductsExist()
    {
        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/inventory");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<InventoryDto>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetInventory_ReturnsList_WhenProductsExist()
    {
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p1 = Product.Create(_retailerId, "Alpha Widget", price: 12.50m);
            var p2 = Product.Create(_retailerId, "Beta Widget", price: 18.00m);
            db.Products.AddRange(p1, p2);
            await db.SaveChangesAsync();

            db.InventoryRecords.AddRange(
                InventoryRecord.Create(_retailerId, p1.Id, p1.Name, 40),
                InventoryRecord.Create(_retailerId, p2.Id, p2.Name, 25));
            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/inventory");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<InventoryDto>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(2);
        result.Data.Items.Should().HaveCount(2);
        result.Data.Items.Should().OnlyContain(i => i.RetailerId == _retailerId);
    }

    [Fact]
    public async Task AdjustStock_IncreasesQuantity_Successfully()
    {
        Guid inventoryId = Guid.Empty;
        const int initialStock = 30;
        const int newStock = 80;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(_retailerId, "Increase Stock Product", price: 15m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            var inv = InventoryRecord.Create(_retailerId, p.Id, p.Name, initialStock);
            db.InventoryRecords.Add(inv);
            await db.SaveChangesAsync();
            inventoryId = inv.Id;
        });

        var request = new AdjustStockRequest(
            NewQuantity: newStock,
            Type: AdjustmentType.ManualIncrease,
            Reason: "Received new shipment");

        var response = await Client.PatchAsJsonAsync(
            $"/api/retailers/{_retailerId}/inventory/{inventoryId}/adjust", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var inv = await db.InventoryRecords.AsNoTracking().FirstAsync(i => i.Id == inventoryId);
            inv.CurrentStock.Should().Be(newStock);
            inv.Status.Should().Be(InventoryStatus.InStock);
        });
    }

    [Fact]
    public async Task AdjustStock_DecreasesQuantity_Successfully()
    {
        Guid inventoryId = Guid.Empty;
        const int initialStock = 50;
        const int newStock = 20;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(_retailerId, "Decrease Stock Product", price: 22m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            var inv = InventoryRecord.Create(_retailerId, p.Id, p.Name, initialStock, lowStockThreshold: 5);
            db.InventoryRecords.Add(inv);
            await db.SaveChangesAsync();
            inventoryId = inv.Id;
        });

        var request = new AdjustStockRequest(
            NewQuantity: newStock,
            Type: AdjustmentType.ManualDecrease,
            Reason: "Damaged goods written off");

        var response = await Client.PatchAsJsonAsync(
            $"/api/retailers/{_retailerId}/inventory/{inventoryId}/adjust", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var inv = await db.InventoryRecords.AsNoTracking().FirstAsync(i => i.Id == inventoryId);
            inv.CurrentStock.Should().Be(newStock);
            inv.Status.Should().Be(InventoryStatus.InStock);
        });
    }

    [Fact]
    public async Task AdjustStock_BelowZero_ShouldReturn422()
    {
        Guid inventoryId = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(_retailerId, "Floor Violation Product", price: 10m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            var inv = InventoryRecord.Create(_retailerId, p.Id, p.Name, 20);
            db.InventoryRecords.Add(inv);
            await db.SaveChangesAsync();
            inventoryId = inv.Id;
        });

        var request = new AdjustStockRequest(
            NewQuantity: -5,
            Type: AdjustmentType.ManualDecrease,
            Reason: "Invalid negative adjustment");

        var response = await Client.PatchAsJsonAsync(
            $"/api/retailers/{_retailerId}/inventory/{inventoryId}/adjust", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var inv = await db.InventoryRecords.AsNoTracking().FirstAsync(i => i.Id == inventoryId);
            inv.CurrentStock.Should().Be(20);
        });
    }

    [Fact]
    public async Task AdjustStock_ForNonExistentProduct_ShouldReturn404()
    {
        var nonExistentInventoryId = Guid.NewGuid();

        var request = new AdjustStockRequest(
            NewQuantity: 10,
            Type: AdjustmentType.ManualIncrease,
            Reason: "Restock attempt for missing product");

        var response = await Client.PatchAsJsonAsync(
            $"/api/retailers/{_retailerId}/inventory/{nonExistentInventoryId}/adjust", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetInventoryByProductId_ValidProduct_ReturnsDetails()
    {
        Guid productId = Guid.Empty;
        Guid inventoryId = Guid.Empty;
        const int initialStock = 55;
        const int threshold = 8;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(_retailerId, "Detailed Stock Product", price: 35m);
            db.Products.Add(p);
            await db.SaveChangesAsync();
            productId = p.Id;

            var inv = InventoryRecord.Create(_retailerId, p.Id, p.Name, initialStock, threshold);
            db.InventoryRecords.Add(inv);
            await db.SaveChangesAsync();
            inventoryId = inv.Id;
        });

        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/inventory/product/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<InventoryDetailDto>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(inventoryId);
        result.Data.ProductId.Should().Be(productId);
        result.Data.RetailerId.Should().Be(_retailerId);
        result.Data.CurrentStock.Should().Be(initialStock);
        result.Data.LowStockThreshold.Should().Be(threshold);
        result.Data.Status.Should().Be(InventoryStatus.InStock);
        result.Data.ProductName.Should().Be("Detailed Stock Product");
    }

    [Fact]
    public async Task GetInventoryByProductId_NonExistentProduct_ShouldReturn404()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/inventory/product/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LowStockThreshold_WhenStockFallsBelow_ShouldTriggerWarning()
    {
        Guid inventoryId = Guid.Empty;
        Guid productId = Guid.Empty;
        const int initialStock = 50;
        const int threshold = 10;
        const int newStockBelowThreshold = 7;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var preference = NotificationPreference.CreateDefault(_retailerId);
            db.NotificationPreferences.Add(preference);

            var p = Product.Create(_retailerId, "Low Stock Watch Product", price: 45m);
            db.Products.Add(p);
            await db.SaveChangesAsync();
            productId = p.Id;

            var inv = InventoryRecord.Create(_retailerId, p.Id, p.Name, initialStock, threshold);
            db.InventoryRecords.Add(inv);
            await db.SaveChangesAsync();
            inventoryId = inv.Id;
        });

        var request = new AdjustStockRequest(
            NewQuantity: newStockBelowThreshold,
            Type: AdjustmentType.ManualDecrease,
            Reason: "Stock corrected after audit");

        var adjustResponse = await Client.PatchAsJsonAsync(
            $"/api/retailers/{_retailerId}/inventory/{inventoryId}/adjust", request);

        adjustResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var inv = await db.InventoryRecords.AsNoTracking().FirstAsync(i => i.Id == inventoryId);
            inv.CurrentStock.Should().Be(newStockBelowThreshold);
            inv.Status.Should().Be(InventoryStatus.LowStock);

            var warning = await db.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(n =>
                    n.RetailerId == _retailerId &&
                    n.Type == Notification.NotificationType.LowStock &&
                    n.ResourceId == productId);

            warning.Should().NotBeNull();
            warning!.IsRead.Should().BeFalse();
            warning.Body.Should().Contain("Low Stock Watch Product");
        });
    }

    [Fact]
    public async Task ExportInventoryCsv_ReturnsValidCsvFile()
    {
        const string productName = "CSV Export Watch";
        const int stock = 42;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(_retailerId, productName, price: 99m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            db.InventoryRecords.Add(InventoryRecord.Create(_retailerId, p.Id, p.Name, stock));
            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/inventory/export/csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        var csvContent = await response.Content.ReadAsStringAsync();
        csvContent.Should().NotBeNullOrWhiteSpace();
        csvContent.Should().Contain("InventoryRecordId,ProductId,ProductName");
        csvContent.Should().Contain("CurrentStock,SoldQuantity,LowStockThreshold");
        csvContent.Should().Contain(productName);
        csvContent.Should().Contain(stock.ToString());
    }
}