using System.Net.Http.Headers;
using Application.Features.Offers.DTOs;
using Domain.Entities.Retailer;
using Domain.Enums.Offer;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class OffersControllerTests : IntegrationTestBase
{
    public OffersControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static MultipartFormDataContent BuildCreateOfferForm(
        string title = "Summer Sale",
        string? description = "Big summer discount",
        string offerType = "Category",
        Guid? productId = null,
        Guid? categoryId = null,
        string discountType = "Percentage",
        string discountValue = "15",
        string? startDate = null,
        string? endDate = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var content = new MultipartFormDataContent();

        content.Add(new StringContent(title), "Title");

        if (description is not null)
            content.Add(new StringContent(description), "Description");

        content.Add(new StringContent(offerType), "OfferType");

        if (productId.HasValue)
            content.Add(new StringContent(productId.Value.ToString()), "ProductId");

        if (categoryId.HasValue)
            content.Add(new StringContent(categoryId.Value.ToString()), "CategoryId");

        content.Add(new StringContent(discountType), "DiscountType");
        content.Add(new StringContent(discountValue), "DiscountValue");
        content.Add(new StringContent(startDate ?? today.ToString("yyyy-MM-dd")), "StartDate");

        if (endDate is not null)
            content.Add(new StringContent(endDate), "EndDate");

        var imageBytes = new ByteArrayContent("fake-image-bytes"u8.ToArray());
        imageBytes.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(imageBytes, "CoverImage", "cover.jpg");

        return content;
    }

    private static MultipartFormDataContent BuildUpdateOfferForm(
        string title = "Updated Offer",
        string? description = "Updated description",
        string discountType = "Percentage",
        string discountValue = "20",
        string status = "Active",
        string? startDate = null,
        string? endDate = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var content = new MultipartFormDataContent();

        content.Add(new StringContent(title), "Title");

        if (description is not null)
            content.Add(new StringContent(description), "Description");

        content.Add(new StringContent(discountType), "DiscountType");
        content.Add(new StringContent(discountValue), "DiscountValue");
        content.Add(new StringContent(startDate ?? today.ToString("yyyy-MM-dd")), "StartDate");

        if (endDate is not null)
            content.Add(new StringContent(endDate), "EndDate");

        content.Add(new StringContent(status), "Status");

        return content;
    }

    private async Task<(Guid categoryId, Guid offerId)> SeedCategoryOfferAsync(
        Guid retailerId,
        string offerTitle = "Seeded Offer",
        decimal discountValue = 10m,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        bool deactivate = false)
    {
        Guid categoryId = Guid.Empty;
        Guid offerId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = Category.Create(
                retailerId,
                "Test Category",
                null,
                "https://cdn.vfr.com/cat.jpg",
                Category.CategoryStatus.Active);
            db.Categories.Add(category);
            await db.SaveChangesAsync();

            var actualStart = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var offer = Offer.Create(
                retailerId,
                offerTitle,
                "Offer description",
                OfferType.Category,
                productId: null,
                categoryId: category.Id,
                DiscountType.Percentage,
                discountValue,
                actualStart,
                endDate,
                "https://cdn.vfr.com/offer.jpg");

            if (deactivate)
                offer.Deactivate();

            db.Set<Offer>().Add(offer);
            await db.SaveChangesAsync();

            categoryId = category.Id;
            offerId = offer.Id;
        });

        return (categoryId, offerId);
    }

    // ── 1. GET /offers ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOffers_ReturnsEmptyList_WhenNoOffersExist()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

        var response = await Client.GetAsync($"/api/retailers/{retailerId}/offers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OfferDto>>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetOffers_ReturnsList_WhenOffersExist()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = Category.Create(
                retailerId,
                "Category A",
                null,
                "https://cdn.vfr.com/a.jpg",
                Category.CategoryStatus.Active);
            db.Categories.Add(category);
            await db.SaveChangesAsync();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            db.Set<Offer>().Add(Offer.Create(
                retailerId, "Offer One", null,
                OfferType.Category, null, category.Id,
                DiscountType.Percentage, 10m,
                today, today.AddDays(30), "https://cdn.vfr.com/o1.jpg"));

            db.Set<Offer>().Add(Offer.Create(
                retailerId, "Offer Two", null,
                OfferType.Category, null, category.Id,
                DiscountType.Percentage, 20m,
                today, today.AddDays(60), "https://cdn.vfr.com/o2.jpg"));

            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync($"/api/retailers/{retailerId}/offers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<OfferDto>>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(2);
        result.Data.Items.Should().HaveCount(2);
        result.Data.Items.Should().Contain(o => o.Title == "Offer One");
        result.Data.Items.Should().Contain(o => o.Title == "Offer Two");
    }

    // ── 2. GET /offers/{offerId} ───────────────────────────────────────────────

    [Fact]
    public async Task GetOfferById_ValidId_ReturnsOffer()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        var (_, offerId) = await SeedCategoryOfferAsync(retailerId, "Flash Sale", discountValue: 25m);

        var response = await Client.GetAsync($"/api/retailers/{retailerId}/offers/{offerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OfferDto>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(offerId);
        result.Data.Title.Should().Be("Flash Sale");
        result.Data.DiscountValue.Should().Be(25m);
        result.Data.OfferType.Should().Be(OfferType.Category);
        result.Data.DiscountType.Should().Be(DiscountType.Percentage);
    }

    [Fact]
    public async Task GetOfferById_InvalidId_ShouldReturn404()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        var nonExistentOfferId = Guid.NewGuid();

        var response = await Client.GetAsync($"/api/retailers/{retailerId}/offers/{nonExistentOfferId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── 3. POST /offers ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOffer_WithValidData_ShouldReturn201()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = Category.Create(
                retailerId, "Active Category", null,
                "https://cdn.vfr.com/cat.jpg", Category.CategoryStatus.Active);
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;
        });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using var form = BuildCreateOfferForm(
            title: "Winter Clearance",
            offerType: OfferType.Category,
            categoryId: categoryId,
            discountType: DiscountType.Percentage,
            discountValue: "30",
            startDate: today.ToString("yyyy-MM-dd"),
            endDate: today.AddDays(14).ToString("yyyy-MM-dd"));

        var response = await Client.PostAsync($"/api/retailers/{retailerId}/offers", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeEmpty();

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var offer = await db.Set<Offer>().FirstOrDefaultAsync(o => o.Id == result.Data);
            offer.Should().NotBeNull();
            offer!.Title.Should().Be("Winter Clearance");
            offer.RetailerId.Should().Be(retailerId);
            offer.DiscountValue.Should().Be(30m);
            offer.Status.Should().Be(OfferStatus.Active);
            offer.CoverImageUrl.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task CreateOffer_WithEndDateBeforeStartDate_ShouldReturn422()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = Category.Create(
                retailerId, "Category For Validation", null,
                "https://cdn.vfr.com/c.jpg", Category.CategoryStatus.Active);
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;
        });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using var form = BuildCreateOfferForm(
            offerType: OfferType.Category,
            categoryId: categoryId,
            discountType: DiscountType.Percentage,
            discountValue: "10",
            startDate: today.AddDays(5).ToString("yyyy-MM-dd"),
            endDate: today.AddDays(2).ToString("yyyy-MM-dd"));

        var response = await Client.PostAsync($"/api/retailers/{retailerId}/offers", form);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateOffer_WithDiscountAbove100_ShouldReturn422()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = Category.Create(
                retailerId, "Discount Test Category", null,
                "https://cdn.vfr.com/d.jpg", Category.CategoryStatus.Active);
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;
        });

        using var form = BuildCreateOfferForm(
            offerType: OfferType.Category,
            categoryId: categoryId,
            discountType: DiscountType.Percentage,
            discountValue: "150");

        var response = await Client.PostAsync($"/api/retailers/{retailerId}/offers", form);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateOffer_ForProductBelongingToDifferentRetailer_ShouldReturn403()
    {
        var otherRetailerId = Guid.NewGuid();
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var otherRetailer = RetailerAccount.Create(
                "Other Retailer",
                "other-offers@test.com",
                DefaultPasswordHash,
                "OtherBrand");

            var idProp = typeof(Domain.Common.BaseEntity).GetProperty(
                nameof(Domain.Common.BaseEntity.Id),
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            idProp!.SetValue(otherRetailer, otherRetailerId);

            db.RetailerAccounts.Add(otherRetailer);

            var category = Category.Create(
                otherRetailerId, "Other Retailer Category", null,
                "https://cdn.vfr.com/other.jpg", Category.CategoryStatus.Active);
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;
        });

        using var form = BuildCreateOfferForm(
            offerType: OfferType.Category,
            categoryId: categoryId);

        var response = await Client.PostAsync($"/api/retailers/{otherRetailerId}/offers", form);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── 4. PUT /offers/{offerId} ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateOffer_WithValidData_ShouldReturn200()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        var (_, offerId) = await SeedCategoryOfferAsync(retailerId, "Original Title", discountValue: 10m);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using var form = BuildUpdateOfferForm(
            title: "Updated Title",
            description: "New description",
            discountType: DiscountType.Percentage,
            discountValue: "35",
            status: OfferStatus.Inactive,
            startDate: today.ToString("yyyy-MM-dd"),
            endDate: today.AddDays(20).ToString("yyyy-MM-dd"));

        var response = await Client.PutAsync($"/api/retailers/{retailerId}/offers/{offerId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var offer = await db.Set<Offer>()
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == offerId);

            offer.Should().NotBeNull();
            offer!.Title.Should().Be("Updated Title");
            offer.DiscountValue.Should().Be(35m);
            offer.Status.Should().Be(OfferStatus.Inactive);
        });
    }

    [Fact]
    public async Task UpdateOffer_ForNonExistentOffer_ShouldReturn404()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        var nonExistentOfferId = Guid.NewGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using var form = BuildUpdateOfferForm(
            startDate: today.ToString("yyyy-MM-dd"),
            endDate: today.AddDays(10).ToString("yyyy-MM-dd"));

        var response = await Client.PutAsync(
            $"/api/retailers/{retailerId}/offers/{nonExistentOfferId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── 5. DELETE /offers/{offerId} ────────────────────────────────────────────

    [Fact]
    public async Task DeleteOffer_WithValidId_ShouldReturn200()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        var (_, offerId) = await SeedCategoryOfferAsync(retailerId, "Offer To Delete");

        var response = await Client.DeleteAsync($"/api/retailers/{retailerId}/offers/{offerId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var offer = await db.Set<Offer>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == offerId);

            offer.Should().NotBeNull();
            offer!.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task DeleteOffer_ForNonExistentOffer_ShouldReturn404()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        var nonExistentOfferId = Guid.NewGuid();

        var response = await Client.DeleteAsync(
            $"/api/retailers/{retailerId}/offers/{nonExistentOfferId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── 6. Expiry ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOfferById_WhenOfferExpired_ReturnsWithExpiredStatus()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        var pastStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
        var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        var (_, offerId) = await SeedCategoryOfferAsync(
            retailerId,
            "Expired Promo",
            discountValue: 15m,
            startDate: pastStart,
            endDate: pastEnd,
            deactivate: true);

        var response = await Client.GetAsync($"/api/retailers/{retailerId}/offers/{offerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OfferDto>>();

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(offerId);
        result.Data.Status.Should().Be(OfferStatus.Expired);
        result.Data.IsExpired.Should().BeTrue();
        result.Data.IsActiveNow.Should().BeFalse();
    }
}