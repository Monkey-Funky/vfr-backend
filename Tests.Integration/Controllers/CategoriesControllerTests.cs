using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using API.Controllers.Categories;
using Application.Features.Categories.DTOs;
using Domain.Entities.Retailer;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

/// <summary>
/// End-to-end integration tests for CategoriesController.
/// Validates category CRUD, sub-category management, and status toggles.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class CategoriesControllerTests : IntegrationTestBase
{
    public CategoriesControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    // ── 1. GET /categories ───────────────────────────────────────────────────

    [Fact]
    public async Task GetCategories_ReturnsPaginatedList_WhenCategoriesExist()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        await Factory.ExecuteDbContextAsync(async db =>
        {
            db.Categories.Add(Category.Create(retailerId, "Electronics", "Devices", "https://img.com/1", Category.CategoryStatus.Active));
            db.Categories.Add(Category.Create(retailerId, "Fashion", "Clothing", "https://img.com/2", Category.CategoryStatus.Active));
            await db.SaveChangesAsync();
        });

        // Act
        var response = await Client.GetAsync($"/api/retailers/{retailerId}/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CategoryDto>>>();

        result!.Data!.Items.Should().HaveCount(2);
        result.Data.TotalCount.Should().Be(2);
    }

    // ── 2. POST /categories ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateCategory_ReturnsCreated_WhenRequestIsValid()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Home & Garden"), "Name");
        content.Add(new StringContent("Decor and plants"), "Description");
        content.Add(new StringContent(Category.CategoryStatus.Active), "Status");

        var fileContent = new ByteArrayContent("fake-image"u8.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "CoverImageFile", "home.jpg");

        // Act
        var response = await Client.PostAsync($"/api/retailers/{retailerId}/categories", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();

        result!.Data.Should().NotBeEmpty();

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = await db.Categories.FindAsync(result.Data);
            category.Should().NotBeNull();
            category!.Name.Should().Be("Home & Garden");
            category.CoverImageUrl.Should().NotBeNullOrWhiteSpace();
        });
    }

    // ── 3. PUT /categories/{categoryId} ──────────────────────────────────────

    [Fact]
    public async Task UpdateCategory_UpdatesFields_WhenRequestIsValid()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Old Name", "Old Desc", "https://img.com/old", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;
        });

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("New Name"), "NewName");
        content.Add(new StringContent("New Desc"), "NewDescription");
        content.Add(new StringContent("true"), "ShouldUpdateDescription");
        content.Add(new StringContent(Category.CategoryStatus.Inactive), "Status");

        // Act
        var response = await Client.PutAsync($"/api/retailers/{retailerId}/categories/{categoryId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = await db.Categories.FindAsync(categoryId);
            category!.Name.Should().Be("New Name");
            category.Description.Should().Be("New Desc");
            category.Status.Should().Be(Category.CategoryStatus.Inactive);
        });
    }

    // ── 4. DELETE /categories/{categoryId} ───────────────────────────────────

    [Fact]
    public async Task DeleteCategory_SoftDeletes_WhenCategoryExists()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "To Delete", null, "https://img.com", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;
        });

        // Act
        var response = await Client.DeleteAsync($"/api/retailers/{retailerId}/categories/{categoryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = await db.Categories.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == categoryId);
            category!.IsDeleted.Should().BeTrue();
        });
    }

    // ── 5. Sub-Categories ────────────────────────────────────────────────────

    [Fact]
    public async Task GetSubCategories_ReturnsList_WhenParentExists()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Electronics", null, "https://img.com", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();

            var sub1 = SubCategory.Create(cat.Id, retailerId, "Smartphones", SubCategory.SubCategoryStatus.Active);
            var sub2 = SubCategory.Create(cat.Id, retailerId, "Tablets", SubCategory.SubCategoryStatus.Active);
            db.SubCategories.AddRange(sub1, sub2);
            await db.SaveChangesAsync();

            categoryId = cat.Id;
        });

        // Act
        var response = await Client.GetAsync($"/api/retailers/{retailerId}/categories/{categoryId}/sub-categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SubCategoryDto>>>();
        result!.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateSubCategory_ReturnsCreated_WhenRequestIsValid()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Main Category", null, "https://img.com", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;
        });

        var request = new CreateSubCategoryRequest
        {
            Name = "Sub Category 1",
            Status = SubCategory.SubCategoryStatus.Active
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/retailers/{retailerId}/categories/{categoryId}/sub-categories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
        result!.Data.Should().NotBeEmpty();

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var sub = await db.SubCategories.FindAsync(result.Data);
            sub.Should().NotBeNull();
            sub!.Name.Should().Be("Sub Category 1");
            sub.CategoryId.Should().Be(categoryId);
        });
    }

    [Fact]
    public async Task GetCategoryById_ReturnsCategory_WhenExists()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Findable Category", "Desc", "https://img.com", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;
        });

        var response = await Client.GetAsync($"/api/retailers/{retailerId}/categories/{categoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CategoryDto>>();
        result!.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Findable Category");
    }

    [Fact]
    public async Task GetCategoryById_ReturnsNotFound_WhenDoesNotExist()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

        var response = await Client.GetAsync($"/api/retailers/{retailerId}/categories/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ToggleCategoryStatus_TogglesFromActiveToInactive_WhenCategoryExists()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Toggle Category", null, "https://img.com", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;
        });

        var response = await Client.PatchAsync(
            $"/api/retailers/{retailerId}/categories/{categoryId}/toggle-status", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = await db.Categories.FindAsync(categoryId);
            category!.Status.Should().Be(Category.CategoryStatus.Inactive);
        });
    }

    [Fact]
    public async Task UpdateSubCategory_UpdatesFields_WhenRequestIsValid()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;
        Guid subCategoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Parent Cat", null, "https://img.com", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;

            var sub = SubCategory.Create(cat.Id, retailerId, "Old Sub Name", SubCategory.SubCategoryStatus.Active);
            db.SubCategories.Add(sub);
            await db.SaveChangesAsync();
            subCategoryId = sub.Id;
        });

        var request = new UpdateSubCategoryRequest
        {
            NewName = "Updated Sub Name",
            Status = SubCategory.SubCategoryStatus.Inactive
        };

        var response = await Client.PutAsJsonAsync(
            $"/api/retailers/{retailerId}/categories/{categoryId}/sub-categories/{subCategoryId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var sub = await db.SubCategories.FindAsync(subCategoryId);
            sub!.Name.Should().Be("Updated Sub Name");
            sub.Status.Should().Be(SubCategory.SubCategoryStatus.Inactive);
        });
    }

    [Fact]
    public async Task DeleteSubCategory_SoftDeletes_WhenSubCategoryExists()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;
        Guid subCategoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Delete Sub Parent", null, "https://img.com", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;

            var sub = SubCategory.Create(cat.Id, retailerId, "Delete Me Sub", SubCategory.SubCategoryStatus.Active);
            db.SubCategories.Add(sub);
            await db.SaveChangesAsync();
            subCategoryId = sub.Id;
        });

        var response = await Client.DeleteAsync(
            $"/api/retailers/{retailerId}/categories/{categoryId}/sub-categories/{subCategoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var sub = await db.SubCategories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == subCategoryId);
            sub!.IsDeleted.Should().BeTrue();
        });
    }
}
