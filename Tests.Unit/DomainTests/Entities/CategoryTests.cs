namespace Tests.Unit.DomainTests.Entities;

public sealed class CategoryTests
{
    private static readonly Guid ValidRetailerId = Guid.NewGuid();
    private const string ValidName = "Electronics";
    private const string ValidCoverImageUrl = "https://cdn.example.com/categories/electronics.jpg";

    private static Category CreateCategory(string name = ValidName, string status = Category.CategoryStatus.Active)
        => Category.Create(ValidRetailerId, name, null, ValidCoverImageUrl, status);

    [Fact]
    public void Create_ValidName_SetsPropertiesCorrectly()
    {
        var retailerId = Guid.NewGuid();
        const string name = "Clothing";
        const string description = "All clothing items";
        const string coverUrl = "https://cdn.example.com/clothing.jpg";

        var category = Category.Create(retailerId, name, description, coverUrl, Category.CategoryStatus.Active);

        category.Id.Should().NotBeEmpty();
        category.RetailerId.Should().Be(retailerId);
        category.Name.Should().Be(name);
        category.Description.Should().Be(description);
        category.CoverImageUrl.Should().Be(coverUrl);
        category.Status.Should().Be(Category.CategoryStatus.Active);
        category.SubCategories.Should().BeEmpty();
    }

    [Fact]
    public void Create_IsActiveByDefault()
    {
        var category = CreateCategory();

        category.Status.Should().Be(Category.CategoryStatus.Active);
    }

    [Fact]
    public void ToggleStatus_FlipsActiveFlag()
    {
        var category = CreateCategory();
        category.Status.Should().Be(Category.CategoryStatus.Active);

        category.ToggleStatus();
        category.Status.Should().Be(Category.CategoryStatus.Inactive);

        category.ToggleStatus();
        category.Status.Should().Be(Category.CategoryStatus.Active);
    }

    [Fact]
    public void Delete_SetsIsDeletedFlag()
    {
        var category = CreateCategory();
        category.IsDeleted.Should().BeFalse();

        category.MarkAsDeleted();

        category.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void AddSubCategory_AddsToCollection()
    {
        var category = CreateCategory();
        var subCategory = SubCategory.Create(category.Id, ValidRetailerId, "Laptops", SubCategory.SubCategoryStatus.Active);

        category.AddSubCategory(subCategory);

        category.SubCategories.Should().ContainSingle();
        category.SubCategories.First().Name.Should().Be("Laptops");
    }

    [Fact]
    public void SubCategoryName_MustBeUnique_ThrowsBusinessRuleException()
    {
        var category = CreateCategory();
        var first = SubCategory.Create(category.Id, ValidRetailerId, "Laptops", SubCategory.SubCategoryStatus.Active);
        var duplicate = SubCategory.Create(category.Id, ValidRetailerId, "Laptops", SubCategory.SubCategoryStatus.Active);
        category.AddSubCategory(first);

        var act = () => category.AddSubCategory(duplicate);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("DUPLICATE_SUBCATEGORY_NAME");
    }
}