namespace API.Controllers.Categories;

/// <summary>JSON body request model for updating a SubCategory. All fields are optional.</summary>
public sealed class UpdateSubCategoryRequest
{
    /// <summary>New name. Null = keep existing.</summary>
    public string? NewName { get; init; }

    /// <summary>New status. Null = keep existing.</summary>
    public string? Status { get; init; }
}