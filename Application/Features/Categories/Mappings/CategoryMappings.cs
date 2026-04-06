using Application.Features.Categories.DTOs;
using Domain.Entities.Retailer;

namespace Application.Features.Categories.Mappings;


/// <summary>
/// Manual mapping extension methods for Category and SubCategory entities.
/// All methods are pure (no I/O, no DI). Called from query handlers after
/// entities are loaded from the database.
/// </summary>
public static class CategoryMappings
{
    /// <summary>
    /// Maps a <see cref="Category"/> to a <see cref="CategoryDto"/>.
    /// <paramref name="subCategoryCount"/> must be supplied by the caller —
    /// this method never queries the database.
    /// </summary>
    public static CategoryDto ToDto(this Category category, int subCategoryCount = 0)
    {
        return new CategoryDto(
            Id: category.Id,
            RetailerId: category.RetailerId,
            Name: category.Name,
            Description: category.Description,
            CoverImageUrl: category.CoverImageUrl,
            Status: category.Status,
            SubCategoryCount: subCategoryCount,
            CreatedAt: category.CreatedAt,
            UpdatedAt: category.UpdatedAt);
    }

    /// <summary>
    /// Maps a <see cref="SubCategory"/> to a <see cref="SubCategoryDto"/>.
    /// </summary>
    public static SubCategoryDto ToDto(this SubCategory subCategory)
    {
        return new SubCategoryDto(
            Id: subCategory.Id,
            CategoryId: subCategory.CategoryId,
            RetailerId: subCategory.RetailerId,
            Name: subCategory.Name,
            Status: subCategory.Status,
            CreatedAt: subCategory.CreatedAt,
            UpdatedAt: subCategory.UpdatedAt);
    }
}