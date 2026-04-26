using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using API.Controllers.Offers.Requests;
using Application.Features.Offers.DTOs;
using Domain.Entities.Retailer;
using Domain.Enums.Offer;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

/// <summary>
/// End-to-end integration tests for OffersController.
/// Validates offer lifecycle: creation (Product/Category), updates, status toggles, and deletion.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class OffersControllerTests : IntegrationTestBase
{
    public OffersControllerTests(CustomWebApplicationFactory factory) 
        : base(factory) 
    { 
    }

    // ── 1. GET /offers ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetOffers_ReturnsPaginatedList_WhenOffersExist()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(retailerId, "P1", price: 100m, status: Domain.Enums.Product.ProductStatus.Active);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            db.Offers.Add(Offer.Create(
                retailerId, "Sale 1", null, OfferType.Product, product.Id, null, 
                DiscountType.Percentage, 10, DateOnly.FromDateTime(DateTime.UtcNow), null, "http://img1.com"));
            
            await db.SaveChangesAsync();
        });

        // Act
        var response = await Client.GetAsync($"/api/retailers/{retailerId}/offers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OfferDto>>>();
        result!.Data!.Items.Should().NotBeEmpty();
    }

    // ── 2. POST /offers ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOffer_ReturnsCreated_WhenProductOfferIsValid()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid productId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var p = Product.Create(retailerId, "Offer Product", price: 500m, status: Domain.Enums.Product.ProductStatus.Active);
            db.Products.Add(p);
            await db.SaveChangesAsync();
            productId = p.Id;
        });

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Flash Sale"), "Title");
        content.Add(new StringContent(OfferType.Product), "OfferType");
        content.Add(new StringContent(productId.ToString()), "ProductId");
        content.Add(new StringContent(DiscountType.Percentage), "DiscountType");
        content.Add(new StringContent("25"), "DiscountValue");
        content.Add(new StringContent(DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")), "StartDate");
        
        var fileContent = new ByteArrayContent("fake-offer-img"u8.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "CoverImage", "flash_sale.jpg");

        // Act
        var response = await Client.PostAsync($"/api/retailers/{retailerId}/offers", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
        result!.Data.Should().NotBeEmpty();

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var offer = await db.Offers.FindAsync(result.Data);
            offer.Should().NotBeNull();
            offer!.Title.Should().Be("Flash Sale");
            offer.ProductId.Should().Be(productId);
        });
    }

    // ── 3. PATCH /offers/{offerId}/toggle-status ─────────────────────────────

    [Fact]
    public async Task ToggleOfferStatus_CyclesStatus_WhenOfferExists()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid offerId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(retailerId, "P1", price: 100m, status: Domain.Enums.Product.ProductStatus.Active);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            var offer = Offer.Create(
                retailerId, "Toggle Me", null, OfferType.Product, product.Id, null, 
                DiscountType.Percentage, 10, DateOnly.FromDateTime(DateTime.UtcNow), null, "http://img.com");
            
            db.Offers.Add(offer);
            await db.SaveChangesAsync();
            offerId = offer.Id;
        });

        // Act - Toggle to Inactive
        var response1 = await Client.PatchAsync($"/api/retailers/{retailerId}/offers/{offerId}/toggle-status", null);
        response1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var offer = await db.Offers.FindAsync(offerId);
            offer!.Status.Should().Be(OfferStatus.Inactive);
        });

        // Act - Toggle back to Active
        var response2 = await Client.PatchAsync($"/api/retailers/{retailerId}/offers/{offerId}/toggle-status", null);
        response2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var offer = await db.Offers.FindAsync(offerId);
            offer!.Status.Should().Be(OfferStatus.Active);
        });
    }

    // ── 4. DELETE /offers/{offerId} ──────────────────────────────────────────

    [Fact]
    public async Task DeleteOffer_SoftDeletes_WhenOfferExists()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid offerId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(retailerId, "P1", price: 100m, status: Domain.Enums.Product.ProductStatus.Active);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            var offer = Offer.Create(
                retailerId, "Delete Me", null, OfferType.Product, product.Id, null, 
                DiscountType.Percentage, 10, DateOnly.FromDateTime(DateTime.UtcNow), null, "http://img.com");
            
            db.Offers.Add(offer);
            await db.SaveChangesAsync();
            offerId = offer.Id;
        });

        // Act
        var response = await Client.DeleteAsync($"/api/retailers/{retailerId}/offers/{offerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var offer = await db.Offers.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == offerId);
            offer!.IsDeleted.Should().BeTrue();
        });
    }
}
