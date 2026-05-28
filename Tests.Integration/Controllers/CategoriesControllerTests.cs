using API.Controllers.Categories;
using Application.Features.Categories.DTOs;
using Domain.Entities.Retailer;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class CategoriesControllerTests : IntegrationTestBase
{
    public CategoriesControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetCategories_ReturnsEmptyList_WhenNoCategoriesExist()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

        var response = await Client.GetAsync($"/api/retailers/{retailerId}/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CategoryDto>>>();
        result!.Data!.Items.Should().BeEmpty();
        result.Data.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetCategories_ReturnsPaginatedList_WhenCategoriesExist()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        await Factory.ExecuteDbContextAsync(async db =>
        {
            db.Categories.Add(Category.Create(retailerId, "Electronics", "Devices", "https://img.com/1", Category.CategoryStatus.Active));
            db.Categories.Add(Category.Create(retailerId, "Fashion", "Clothing", "https://img.com/2", Category.CategoryStatus.Active));
            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync($"/api/retailers/{retailerId}/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CategoryDto>>>();
        result!.Data!.Items.Should().HaveCount(2);
        result.Data.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateCategory_WithValidData_ShouldReturn201()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Sports & Outdoors"), "Name");
        content.Add(new StringContent("Sports equipment and outdoor gear"), "Description");
        content.Add(new StringContent(Category.CategoryStatus.Active), "Status");

        var fileContent = new ByteArrayContent("fake-image-bytes"u8.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "CoverImageFile", "sports.jpg");

        var response = await Client.PostAsync($"/api/retailers/{retailerId}/categories", content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
        result!.Data.Should().NotBeEmpty();

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = await db.Categories.FindAsync(result.Data);
            category.Should().NotBeNull();
            category!.Name.Should().Be("Sports & Outdoors");
            category.RetailerId.Should().Be(retailerId);
        });
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateName_ShouldReturn409()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        await Factory.ExecuteDbContextAsync(async db =>
        {
            db.Categories.Add(Category.Create(
                retailerId, "Duplicate Name", null, "https://img.com/dup", Category.CategoryStatus.Active));
            await db.SaveChangesAsync();
        });

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Duplicate Name"), "Name");
        content.Add(new StringContent(Category.CategoryStatus.Active), "Status");

        var fileContent = new ByteArrayContent("fake-image-bytes"u8.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "CoverImageFile", "dup.jpg");

        var response = await Client.PostAsync($"/api/retailers/{retailerId}/categories", content);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateSubCategory_UnderExistingCategory_ShouldReturn201()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Parent Category", null, "https://img.com/p", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;
        });

        var request = new CreateSubCategoryRequest
        {
            Name = "Child SubCategory",
            Status = SubCategory.SubCategoryStatus.Active
        };

        var response = await Client.PostAsJsonAsync(
            $"/api/retailers/{retailerId}/categories/{categoryId}/sub-categories", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
        result!.Data.Should().NotBeEmpty();

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var sub = await db.SubCategories.FindAsync(result.Data);
            sub.Should().NotBeNull();
            sub!.Name.Should().Be("Child SubCategory");
            sub.CategoryId.Should().Be(categoryId);
            sub.RetailerId.Should().Be(retailerId);
        });
    }

    [Fact]
    public async Task GetSubCategories_ReturnsCorrectSubCategories()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Tech Category", null, "https://img.com/t", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();

            db.SubCategories.AddRange(
                SubCategory.Create(cat.Id, retailerId, "Laptops", SubCategory.SubCategoryStatus.Active),
                SubCategory.Create(cat.Id, retailerId, "Monitors", SubCategory.SubCategoryStatus.Active),
                SubCategory.Create(cat.Id, retailerId, "Keyboards", SubCategory.SubCategoryStatus.Inactive));
            await db.SaveChangesAsync();

            categoryId = cat.Id;
        });

        var response = await Client.GetAsync(
            $"/api/retailers/{retailerId}/categories/{categoryId}/sub-categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SubCategoryDto>>>();
        result!.Data.Should().HaveCount(3);
        result.Data!.Select(s => s.Name).Should().BeEquivalentTo(["Laptops", "Monitors", "Keyboards"]);
        result.Data.All(s => s.CategoryId == categoryId).Should().BeTrue();
        result.Data.All(s => s.RetailerId == retailerId).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCategory_WithValidId_ShouldReturn200()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Category To Delete", null, "https://img.com/del", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;
        });

        var response = await Client.DeleteAsync($"/api/retailers/{retailerId}/categories/{categoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = await db.Categories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == categoryId);
            category!.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task DeleteCategory_WithAttachedProducts_ShouldReturn422()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;
        Guid productId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(
                retailerId, "Category With Products", null, "https://img.com/cwp", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;

            var product = Product.Create(
                retailerId: retailerId,
                name: "Attached Product",
                categoryId: cat.Id);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });

        var response = await Client.DeleteAsync($"/api/retailers/{retailerId}/categories/{categoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = await db.Products
                .IgnoreQueryFilters()
                .FirstAsync(p => p.Id == productId);
            product.CategoryId.Should().BeNull();

            var category = await db.Categories
                .IgnoreQueryFilters()
                .FirstAsync(c => c.Id == categoryId);
            category.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task ToggleCategoryStatus_ShouldFlipActiveFlag()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Toggle Me", null, "https://img.com/tog", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;
        });

        var response = await Client.PatchAsync(
            $"/api/retailers/{retailerId}/categories/{categoryId}/toggle-status", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CategoryStatusDto>>();
        result!.Data!.NewStatus.Should().Be(Category.CategoryStatus.Inactive);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = await db.Categories.FindAsync(categoryId);
            category!.Status.Should().Be(Category.CategoryStatus.Inactive);
        });

        var secondToggle = await Client.PatchAsync(
            $"/api/retailers/{retailerId}/categories/{categoryId}/toggle-status", null);

        secondToggle.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondResult = await secondToggle.Content.ReadFromJsonAsync<ApiResponse<CategoryStatusDto>>();
        secondResult!.Data!.NewStatus.Should().Be(Category.CategoryStatus.Active);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = await db.Categories.FindAsync(categoryId);
            category!.Status.Should().Be(Category.CategoryStatus.Active);
        });
    }

    [Fact]
    public async Task UpdateCategory_UpdatesFields_WhenRequestIsValid()
    {
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

        var response = await Client.PutAsync($"/api/retailers/{retailerId}/categories/{categoryId}", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var category = await db.Categories.FindAsync(categoryId);
            category!.Name.Should().Be("New Name");
            category.Description.Should().Be("New Desc");
            category.Status.Should().Be(Category.CategoryStatus.Inactive);
        });
    }

    [Fact]
    public async Task GetCategoryById_ReturnsCategory_WhenExists()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Findable Category", "Desc", "https://img.com/f", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;
        });

        var response = await Client.GetAsync($"/api/retailers/{retailerId}/categories/{categoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CategoryDto>>();
        result!.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Findable Category");
        result.Data.RetailerId.Should().Be(retailerId);
    }

    [Fact]
    public async Task GetCategoryById_ReturnsNotFound_WhenDoesNotExist()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

        var response = await Client.GetAsync($"/api/retailers/{retailerId}/categories/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateSubCategory_WithDuplicateName_ShouldReturn409()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Unique Parent", null, "https://img.com/up", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();

            db.SubCategories.Add(SubCategory.Create(cat.Id, retailerId, "Existing Sub", SubCategory.SubCategoryStatus.Active));
            await db.SaveChangesAsync();
            categoryId = cat.Id;
        });

        var request = new CreateSubCategoryRequest
        {
            Name = "Existing Sub",
            Status = SubCategory.SubCategoryStatus.Active
        };

        var response = await Client.PostAsJsonAsync(
            $"/api/retailers/{retailerId}/categories/{categoryId}/sub-categories", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteCategory_WhenCategoryDoesNotExist_ReturnsNotFound()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

        var response = await Client.DeleteAsync(
            $"/api/retailers/{retailerId}/categories/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSubCategories_ReturnsList_WhenParentExists()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Electronics", null, "https://img.com/e", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();

            db.SubCategories.AddRange(
                SubCategory.Create(cat.Id, retailerId, "Smartphones", SubCategory.SubCategoryStatus.Active),
                SubCategory.Create(cat.Id, retailerId, "Tablets", SubCategory.SubCategoryStatus.Active));
            await db.SaveChangesAsync();

            categoryId = cat.Id;
        });

        var response = await Client.GetAsync(
            $"/api/retailers/{retailerId}/categories/{categoryId}/sub-categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SubCategoryDto>>>();
        result!.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateSubCategory_ReturnsCreated_WhenRequestIsValid()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Main Category", null, "https://img.com/m", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;
        });

        var request = new CreateSubCategoryRequest
        {
            Name = "Sub Category 1",
            Status = SubCategory.SubCategoryStatus.Active
        };

        var response = await Client.PostAsJsonAsync(
            $"/api/retailers/{retailerId}/categories/{categoryId}/sub-categories", request);

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
    public async Task UpdateSubCategory_UpdatesFields_WhenRequestIsValid()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;
        Guid subCategoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Parent Cat", null, "https://img.com/pc", Category.CategoryStatus.Active);
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
            var cat = Category.Create(retailerId, "Delete Sub Parent", null, "https://img.com/dsp", Category.CategoryStatus.Active);
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

    [Fact]
    public async Task DeleteCategory_CascadesSoftDeleteToSubCategories()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;
        Guid subId1 = Guid.Empty;
        Guid subId2 = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(
                retailerId, "Cascade Parent", null, "https://img.com/cp", Category.CategoryStatus.Active);
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;

            var s1 = SubCategory.Create(cat.Id, retailerId, "Sub One", SubCategory.SubCategoryStatus.Active);
            var s2 = SubCategory.Create(cat.Id, retailerId, "Sub Two", SubCategory.SubCategoryStatus.Active);
            db.SubCategories.AddRange(s1, s2);
            await db.SaveChangesAsync();
            subId1 = s1.Id;
            subId2 = s2.Id;
        });

        var response = await Client.DeleteAsync($"/api/retailers/{retailerId}/categories/{categoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var sub1 = await db.SubCategories.IgnoreQueryFilters().FirstAsync(s => s.Id == subId1);
            var sub2 = await db.SubCategories.IgnoreQueryFilters().FirstAsync(s => s.Id == subId2);
            sub1.IsDeleted.Should().BeTrue();
            sub2.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task ToggleCategoryStatus_TogglesFromActiveToInactive_WhenCategoryExists()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid categoryId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var cat = Category.Create(retailerId, "Toggle Category", null, "https://img.com/tc", Category.CategoryStatus.Active);
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
    public async Task CreateCategory_ReturnsCreated_WhenRequestIsValid()
    {
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Home & Garden"), "Name");
        content.Add(new StringContent("Decor and plants"), "Description");
        content.Add(new StringContent(Category.CategoryStatus.Active), "Status");

        var fileContent = new ByteArrayContent("fake-image"u8.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "CoverImageFile", "home.jpg");

        var response = await Client.PostAsync($"/api/retailers/{retailerId}/categories", content);

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
}