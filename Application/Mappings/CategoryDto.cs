
namespace Application.Mappings;


/// <summary>
/// Flat projection of a Category — returned by GetCategoriesQuery and GetCategoryByIdQuery.
/// </summary>
public sealed record CategoryDto(
    Guid Id,
    Guid RetailerId,
    string Name,
    string? Description,
    string CoverImageUrl,
    string Status,
    int SubCategoryCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Flat projection of a SubCategory — returned by GetSubCategoriesQuery.
/// </summary>
public sealed record SubCategoryDto(
    Guid Id,
    Guid CategoryId,
    Guid RetailerId,
    string Name,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Lightweight response returned by ToggleCategoryStatusCommand to inform
/// the caller of the newly applied status without requiring a re-fetch.
/// </summary>
public sealed record CategoryStatusDto(string NewStatus);
