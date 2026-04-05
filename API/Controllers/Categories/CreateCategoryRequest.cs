using static Domain.Entities.Retailer.Category;

namespace API.Controllers.Categories;


/// <summary>HTTP form-data request model for creating a Category.</summary>
public sealed class CreateCategoryRequest
{
    /// <summary>Category display name. Required. Max 150 characters.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Cover image file. Required. Max 1 MB. JPEG/PNG/WebP only.</summary>
    public IFormFile CoverImageFile { get; init; } = null!;

    /// <summary>Initial status. Must be 'Active' or 'Inactive'.</summary>
    public string Status { get; init; } = CategoryStatus.Active;
}