using Domain.Enums.Product;

namespace Tests.Unit.DomainTests.Entities;

/// <summary>
/// Unit tests for <see cref="Product"/> aggregate root.
/// Validates factory method, domain methods, and business rule enforcement.
/// </summary>
public sealed class ProductTests
{
    private static readonly Guid ValidRetailerId = Guid.NewGuid();

    // ── Factory Method: Create ───────────────────────────────────────────────

    [Fact]
    public void Create_ValidInput_ReturnsProductWithCorrectProperties()
    {
        // Act
        var product = Product.Create(
            retailerId: ValidRetailerId,
            name: "  Test Product  ",
            description: "A great product",
            price: 99.99m,
            currency: "EGP",
            barcode: "ABC123",
            status: ProductStatus.Draft);

        // Assert
        product.Should().NotBeNull();
        product.Id.Should().NotBe(Guid.Empty);
        product.RetailerId.Should().Be(ValidRetailerId);
        product.Name.Should().Be("Test Product"); // trimmed
        product.Description.Should().Be("A great product");
        product.Price.Should().Be(99.99m);
        product.Currency.Should().Be("EGP");
        product.Barcode.Should().Be("ABC123");
        product.Status.Should().Be(ProductStatus.Draft);
        product.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_EmptyRetailerId_ThrowsArgumentException()
    {
        // Act
        var act = () => Product.Create(
            retailerId: Guid.Empty,
            name: "Test Product");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("retailerId");
    }

    [Fact]
    public void Create_NullOrWhitespaceName_ThrowsArgumentException()
    {
        // Act
        var act = () => Product.Create(
            retailerId: ValidRetailerId,
            name: "   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_InvalidStatus_ThrowsBusinessRuleException()
    {
        // Act
        var act = () => Product.Create(
            retailerId: ValidRetailerId,
            name: "Test",
            status: "InvalidStatus");

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("INVALID_PRODUCT_STATUS");
    }

    [Fact]
    public void Create_NegativePrice_ThrowsBusinessRuleException()
    {
        // Act
        var act = () => Product.Create(
            retailerId: ValidRetailerId,
            name: "Test",
            price: -1m);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("INVALID_PRODUCT_PRICE");
    }

    [Fact]
    public void Create_ZeroPrice_ThrowsBusinessRuleException()
    {
        // Act
        var act = () => Product.Create(
            retailerId: ValidRetailerId,
            name: "Test",
            price: 0m);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("INVALID_PRODUCT_PRICE");
    }

    // ── ToggleStatus ─────────────────────────────────────────────────────────

    [Fact]
    public void ToggleStatus_FromActive_SwitchesToInactive()
    {
        // Arrange
        var product = Product.Create(ValidRetailerId, "Test", status: ProductStatus.Active);

        // Act
        var newStatus = product.ToggleStatus();

        // Assert
        newStatus.Should().Be(ProductStatus.Inactive);
        product.Status.Should().Be(ProductStatus.Inactive);
    }

    [Fact]
    public void ToggleStatus_FromDraft_SwitchesToActive()
    {
        // Arrange
        var product = Product.Create(ValidRetailerId, "Test", status: ProductStatus.Draft);

        // Act
        var newStatus = product.ToggleStatus();

        // Assert
        newStatus.Should().Be(ProductStatus.Active);
        product.Status.Should().Be(ProductStatus.Active);
    }

    // ── SoftDelete ───────────────────────────────────────────────────────────

    [Fact]
    public void SoftDelete_SetsIsDeletedToTrue()
    {
        // Arrange
        var product = Product.Create(ValidRetailerId, "Test");

        // Act
        product.SoftDelete();

        // Assert
        product.IsDeleted.Should().BeTrue();
    }

    // ── Images ───────────────────────────────────────────────────────────────

    [Fact]
    public void AddImage_ValidInput_AddsToCollection()
    {
        // Arrange
        var product = Product.Create(ValidRetailerId, "Test");
        var url = "https://s3.amazonaws.com/vfr/image.jpg";

        // Act
        product.AddImage(url, 0);

        // Assert
        product.Images.Should().HaveCount(1);
        product.Images.First().ImageUrl.Should().Be(url);
        product.Images.First().DisplayOrder.Should().Be(0);
    }

    [Fact]
    public void AddImage_NegativeDisplayOrder_ThrowsBusinessRuleException()
    {
        // Arrange
        var product = Product.Create(ValidRetailerId, "Test");

        // Act
        var act = () => product.AddImage("https://url.com", -1);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("INVALID_DISPLAY_ORDER");
    }

    [Fact]
    public void RemoveImage_ExistingImage_SoftDeletes()
    {
        // Arrange
        var product = Product.Create(ValidRetailerId, "Test");
        product.AddImage("https://url.com", 0);
        var imageId = product.Images.First().Id;

        // Act
        product.RemoveImage(imageId);

        // Assert
        product.Images.First().IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void RemoveImage_NonExistent_ThrowsNotFoundException()
    {
        // Arrange
        var product = Product.Create(ValidRetailerId, "Test");

        // Act
        var act = () => product.RemoveImage(Guid.NewGuid());

        // Assert
        act.Should().Throw<NotFoundException>();
    }

    // ── UpdateCategory ───────────────────────────────────────────────────────

    [Fact]
    public void UpdateCategory_SubCategoryWithoutCategory_ThrowsBusinessRuleException()
    {
        // Arrange
        var product = Product.Create(ValidRetailerId, "Test");

        // Act
        var act = () => product.UpdateCategory(
            categoryId: null,
            subCategoryId: Guid.NewGuid());

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("SUB_CATEGORY_WITHOUT_CATEGORY");
    }
}
