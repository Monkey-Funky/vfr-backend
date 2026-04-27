using System.Net;
using System.Net.Http.Json;
using API.Controllers.Inventory;
using Application.Features.Inventory.DTOs;
using Domain.Entities.Retailer;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

/// <summary>
/// End-to-end integration tests for InventoryController.
/// Validates stock listing, adjustments (manual), threshold updates, and CSV export.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class InventoryControllerTests : IntegrationTestBase
{
    public InventoryControllerTests(CustomWebApplicationFactory factory) 
        : base(factory) 
    { 
    }

    // ── 1. GET /inventory ───────────────────────────────────────────────────

    [Fact]
    public async Task GetInventory_ReturnsPaginatedList_WhenRecordsExist()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(retailerId, "Product A", price: 10m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            db.InventoryRecords.Add(InventoryRecord.Create(retailerId, p.Id, p.Name, 100));
            await db.SaveChangesAsync();
        });

        // Act
        var response = await Client.GetAsync($"/api/retailers/{retailerId}/inventory");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<InventoryDto>>>();
        result!.Data!.Items.Should().NotBeEmpty();
        result.Data.Items[0].CurrentStock.Should().Be(100);
    }

    // ── 2. PATCH /inventory/{id}/adjust ──────────────────────────────────────

    [Fact]
    public async Task AdjustStock_UpdatesQuantity_WhenRequestIsValid()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid inventoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(retailerId, "Product B", price: 20m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            var inv = InventoryRecord.Create(retailerId, p.Id, p.Name, 50);
            db.InventoryRecords.Add(inv);
            await db.SaveChangesAsync();
            inventoryId = inv.Id;
        });

        var request = new AdjustStockRequest(
            NewQuantity: 75,
            Type: AdjustmentType.ManualIncrease,
            Reason: "Stock delivery"
        );

        // Act
        var response = await Client.PatchAsJsonAsync($"/api/retailers/{retailerId}/inventory/{inventoryId}/adjust", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var inv = await db.InventoryRecords.FindAsync(inventoryId);
            inv!.CurrentStock.Should().Be(75);
        });
    }

    // ── 3. PUT /inventory/{id}/threshold ─────────────────────────────────────

    [Fact]
    public async Task SetLowStockThreshold_UpdatesThreshold_WhenRequestIsValid()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid inventoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(retailerId, "Product C", price: 30m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            var inv = InventoryRecord.Create(retailerId, p.Id, p.Name, 50, lowStockThreshold: 10);
            db.InventoryRecords.Add(inv);
            await db.SaveChangesAsync();
            inventoryId = inv.Id;
        });

        var request = new SetLowStockThresholdRequest(25);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/retailers/{retailerId}/inventory/{inventoryId}/threshold", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var inv = await db.InventoryRecords.FindAsync(inventoryId);
            inv!.LowStockThreshold.Should().Be(25);
        });
    }

    // ── 4. GET /inventory/export/csv ─────────────────────────────────────────

    [Fact]
    public async Task ExportCsv_ReturnsFile_WhenCalled()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(retailerId, "Export Product", price: 10m);
            db.Products.Add(p);
            await db.SaveChangesAsync();

            db.InventoryRecords.Add(InventoryRecord.Create(retailerId, p.Id, p.Name, 10));
            await db.SaveChangesAsync();
        });

        // Act
        var response = await Client.GetAsync($"/api/retailers/{retailerId}/inventory/export/csv");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Export Product");
    }
}
