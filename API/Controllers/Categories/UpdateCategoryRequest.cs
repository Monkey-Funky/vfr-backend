namespace API.Controllers.Categories;


/// <summary>HTTP form-data request model for updating a Category. All fields are optional.</summary>
public sealed class UpdateCategoryRequest
{
    /// <summary>New name. Null = keep existing.</summary>
    public string? NewName { get; init; }

    /// <summary>New description. Applied only when <see cref="ShouldUpdateDescription"/> is true.</summary>
    public string? NewDescription { get; init; }

    /// <summary>
    /// Set to true to update the description (even to null/empty = clear it).
    /// Set to false (default) to leave the existing description unchanged.
    /// </summary>
    public bool ShouldUpdateDescription { get; init; }

    /// <summary>New cover image. Null = keep existing image.</summary>
    public IFormFile? NewCoverImageFile { get; init; }

    /// <summary>New status. Null = keep existing.</summary>
    public string? Status { get; init; }
}