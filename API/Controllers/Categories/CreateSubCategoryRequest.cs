namespace API.Controllers.Categories;

/// <summary>JSON body request model for creating a SubCategory.</summary>
public sealed class CreateSubCategoryRequest
{
    /// <summary>Sub-category name. Required. Max 150 characters.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Initial status. Must be 'Active' or 'Inactive'.</summary>
    public string Status { get; init; } = Domain.Entities.Retailer.SubCategory.SubCategoryStatus.Active;
}
