using System.Net.Http.Json;
using System.Net.Http.Headers;
using Application.Features.Products.DTOs;
using API.Controllers.Products.Requests;
using Domain.Constants;
using Domain.Entities.Retailer;
using Domain.Enums.Product;
using Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace Tests.Integration.Controllers;

/// <summary>
/// End-to-end integration tests for ProductsController.
/// Validates CRUD operations, lifecycle management, and security guards.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ProductsControllerTests : IntegrationTestBase
{
    public ProductsControllerTests(CustomWebApplicationFactory factory) 
        : base(factory) 
    { 
    }

    // ── 1. GET /products ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetProducts_ReturnsEmptyList_WhenNoProductsExist()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

        // Act
        var response = await Client.GetAsync($"/api/retailers/{retailerId}/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProductListDto>>>();
        
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetProducts_ReturnsForbidden_WhenRetailerIdDoesNotMatchJwt()
    {
        // Arrange
        var otherRetailerId = Guid.NewGuid(); // different from DefaultRetailerId in JWT

        // Act
        var response = await Client.GetAsync($"/api/retailers/{otherRetailerId}/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── 2. POST /products ─────────────────────────────────────────────────────

    //[Fact]
    //public async Task CreateProduct_ReturnsCreated_WhenRequestIsValid()
    //{
    //    // Arrange
    //    var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        
    //    // ProductsController consumes multipart/form-data
    //    using var content = new MultipartFormDataContent();
    //    content.Add(new StringContent("Integration Test Product"), "Name");
    //    content.Add(new StringContent("Premium quality testing item"), "Description");
    //    content.Add(new StringContent("299.50"), "Price");
    //    content.Add(new StringContent("EGP"), "Currency");
    //    content.Add(new StringContent("BC-12345"), "Barcode");
    //    content.Add(new StringContent("100"), "InitialQuantity");
    //    content.Add(new StringContent(ProductStatus.Active), "Status");

    //    // Act
    //    var response = await Client.PostAsync($"/api/retailers/{retailerId}/products", content);

    //    // Assert
    //    response.StatusCode.Should().Be(HttpStatusCode.Created);
    //    var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDetailDto>>();
        
    //    result.Should().NotBeNull();
    //    result!.Success.Should().BeTrue();
    //    result.Data!.Name.Should().Be("Integration Test Product");
    //    result.Data.Price.Should().Be(299.50m);
    //    result.Data.Status.Should().Be(ProductStatus.Active);
        
    //    // Verify persistent state in DB
    //    await Factory.ExecuteDbContextAsync(async db =>
    //    {
    //        var product = await db.Products
    //            .Include(p => p.Images)
    //            .FirstOrDefaultAsync(p => p.Id == result.Data.Id);
            
    //        product.Should().NotBeNull();
    //        product!.Name.Should().Be("Integration Test Product");
    //        product.RetailerId.Should().Be(retailerId);
            
    //        var inventory = await db.InventoryRecords
    //            .FirstOrDefaultAsync(i => i.ProductId == product.Id);
            
    //        inventory.Should().NotBeNull();
    //        inventory!.CurrentStock.Should().Be(100);
    //    });
    //}

    // ── 3. GET /products/{productId} ──────────────────────────────────────────

    [Fact]
    public async Task GetProductById_ReturnsProduct_WhenProductExists()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid productId = Guid.Empty;
        
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(
                retailerId, 
                "Existing Test Product", 
                price: 150m, 
                status: ProductStatus.Active);
            
            db.Products.Add(product);
            
            // Manual inventory seed (usually handled by CreateProductCommand)
            var inventory = InventoryRecord.Create(retailerId, product.Id, product.Name, 50, 10);
            db.InventoryRecords.Add(inventory);
            
            await db.SaveChangesAsync();
            productId = product.Id;
        });

        // Act
        var response = await Client.GetAsync($"/api/retailers/{retailerId}/products/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDetailDto>>();
        
        result.Should().NotBeNull();
        result!.Data!.Id.Should().Be(productId);
        result.Data.Name.Should().Be("Existing Test Product");
        result.Data.CurrentStock.Should().Be(50);
    }

    [Fact]
    public async Task GetProductById_ReturnsNotFound_WhenProductExistsButBelongsToOtherRetailer()
    {
        // Arrange
        var currentRetailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        var otherRetailerId = Guid.NewGuid();
        Guid otherProductId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var otherRetailer = RetailerAccount.Create("Other", "other@test.com", "hash", "OtherBrand");
            db.RetailerAccounts.Add(otherRetailer);
            
            // Force ID
            var idProperty = typeof(Domain.Common.BaseEntity).GetProperty("Id", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            idProperty!.SetValue(otherRetailer, otherRetailerId);

            var product = Product.Create(otherRetailerId, "Other Retailer's Product");
            db.Products.Add(product);
            await db.SaveChangesAsync();
            otherProductId = product.Id;
        });

        // Act - Trying to access other retailer's product using current retailer's ID in URL
        // Note: ProductsController uses EnsureRetailerOwnership(retailerId) first,
        // then the handler filters by productId. If we pass currentRetailerId in URL
        // but otherProductId, the handler will not find it for currentRetailerId.
        var response = await Client.GetAsync($"/api/retailers/{currentRetailerId}/products/{otherProductId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── 4. PUT /products/{productId} ──────────────────────────────────────────

    [Fact]
    public async Task UpdateProduct_UpdatesFields_WhenRequestIsValid()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid productId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(retailerId, "Old Name", price: 10.0m);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });

        var request = new UpdateProductRequest(
            NewName: "New Premium Name",
            NewDescription: "Updated description",
            ShouldUpdateDescription: true,
            NewPrice: 19.99m,
            ShouldUpdatePrice: true,
            NewBarcode: null,
            ShouldUpdateBarcode: false,
            NewCategoryId: null,
            ShouldUpdateCategory: false,
            NewSubCategoryId: null,
            NewStatus: ProductStatus.Inactive
        );

        // Act
        var response = await Client.PutAsJsonAsync($"/api/retailers/{retailerId}/products/{productId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDetailDto>>();
        
        result!.Data!.Name.Should().Be("New Premium Name");
        result.Data.Price.Should().Be(19.99m);
        result.Data.Status.Should().Be(ProductStatus.Inactive);
        result.Data.Description.Should().Be("Updated description");
    }

    // ── 5. DELETE /products/{productId} ───────────────────────────────────────

    [Fact]
    public async Task DeleteProduct_SoftDeletesProduct_WhenProductExists()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid productId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(retailerId, "Product to Delete");
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });

        // Act
        var response = await Client.DeleteAsync($"/api/retailers/{retailerId}/products/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify soft delete in DB
        await Factory.ExecuteDbContextAsync(async db =>
        {
            // Use IgnoreQueryFilters to find the soft-deleted entity
            var product = await db.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == productId);
            
            product.Should().NotBeNull();
            product!.IsDeleted.Should().BeTrue();
        });
    }

    // ── 6. PATCH /products/{productId}/status ─────────────────────────────────

    [Fact]
    public async Task ToggleProductStatus_CyclesStatus_WhenProductExists()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid productId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(retailerId, "Lifecycle Product", status: ProductStatus.Draft);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });

        // Act 1: Draft -> Active
        var response1 = await Client.PatchAsync($"/api/retailers/{retailerId}/products/{productId}/status", null);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        var result1 = await response1.Content.ReadFromJsonAsync<ApiResponse<string>>();
        result1!.Data.Should().Be(ProductStatus.Active);

        // Act 2: Active -> Inactive
        var response2 = await Client.PatchAsync($"/api/retailers/{retailerId}/products/{productId}/status", null);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        var result2 = await response2.Content.ReadFromJsonAsync<ApiResponse<string>>();
        result2!.Data.Should().Be(ProductStatus.Inactive);

        // Act 3: Inactive -> Active
        var response3 = await Client.PatchAsync($"/api/retailers/{retailerId}/products/{productId}/status", null);
        response3.StatusCode.Should().Be(HttpStatusCode.OK);
        var result3 = await response3.Content.ReadFromJsonAsync<ApiResponse<string>>();
        result3!.Data.Should().Be(ProductStatus.Active);
    }

    // ── 7. PATCH /products/{productId} ───────────────────────────────────────

    [Fact]
    public async Task PatchProduct_UpdatesPartialFields_WhenRequestIsValid()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid productId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(retailerId, "Original Name", price: 100m);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });

        // Only update the name, leave price as-is
        var request = new UpdateProductRequest(
            NewName: "Patched Name",
            NewDescription: null,
            ShouldUpdateDescription: false,
            NewPrice: null,
            ShouldUpdatePrice: false,
            NewBarcode: null,
            ShouldUpdateBarcode: false,
            NewCategoryId: null,
            ShouldUpdateCategory: false,
            NewSubCategoryId: null,
            NewStatus: null
        );

        // Act
        var response = await Client.PatchAsJsonAsync($"/api/retailers/{retailerId}/products/{productId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDetailDto>>();
        
        result!.Data!.Name.Should().Be("Patched Name");
        result.Data.Price.Should().Be(100m); // Unchanged
    }

    // ── 8. GET /products/{productId}/images ───────────────────────────────────

    [Fact]
    public async Task GetProductImages_ReturnsImages_WhenProductHasImages()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid productId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(retailerId, "Product with Images");
            product.AddImage("https://s3.com/image1.jpg", 1);
            product.AddImage("https://s3.com/image2.jpg", 0); // Should come first
            
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        });

        // Act
        var response = await Client.GetAsync($"/api/retailers/{retailerId}/products/{productId}/images");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductImageDto>>>();
        
        result!.Data.Should().HaveCount(2);
        result.Data![0].ImageUrl.Should().Be("https://s3.com/image2.jpg");
        result.Data[1].ImageUrl.Should().Be("https://s3.com/image1.jpg");
    }

    // ── 9. POST /products/{productId}/images ──────────────────────────────────

    //[Fact]
    //public async Task AddProductImage_ReturnsCreated_WhenRequestIsValid()
    //{
    //    // Arrange
    //    var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
    //    Guid productId = Guid.Empty;

    //    await Factory.ExecuteDbContextAsync(async db =>
    //    {
    //        var product = Product.Create(retailerId, "Product for Image Upload");
    //        db.Products.Add(product);
    //        await db.SaveChangesAsync();
    //        productId = product.Id;
    //    });

    //    using var content = new MultipartFormDataContent();
    //    var fileContent = new ByteArrayContent("fake-image-binary"u8.ToArray());
    //    fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
    //    content.Add(fileContent, "ImageFile", "test.jpg");
    //    content.Add(new StringContent("5"), "DisplayOrder");

    //    // Act
    //    var response = await Client.PostAsync($"/api/retailers/{retailerId}/products/{productId}/images", content);

    //    // Assert
    //    response.StatusCode.Should().Be(HttpStatusCode.Created);
    //    var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProductImageDto>>();
        
    //    result!.Data!.DisplayOrder.Should().Be(5);
    //    result.Data.ImageUrl.Should().NotBeNullOrWhiteSpace();

    //    // Verify in DB
    //    await Factory.ExecuteDbContextAsync(async db =>
    //    {
    //        var product = await db.Products.Include(p => p.Images).FirstAsync(p => p.Id == productId);
    //        product.Images.Should().ContainSingle(i => i.Id == result.Data.Id);
    //    });
    //}

    // ── 10. DELETE /products/{productId}/images/{imageId} ─────────────────────

    [Fact]
    public async Task RemoveProductImage_SoftDeletesImage_WhenImageExists()
    {
        // Arrange
        var retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);
        Guid productId = Guid.Empty;
        Guid imageId = Guid.Empty;

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var product = Product.Create(retailerId, "Product with Image to Delete");
            product.AddImage("https://s3.com/delete-me.jpg", 1);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            
            productId = product.Id;
            imageId = product.Images.First().Id;
        });

        // Act
        var response = await Client.DeleteAsync($"/api/retailers/{retailerId}/products/{productId}/images/{imageId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify soft delete in DB
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var image = await db.Set<ProductImage>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == imageId);
            
            image.Should().NotBeNull();
            image!.IsDeleted.Should().BeTrue();
        });
    }
}
