using Application.Features.Products.DTOs;

namespace Application.Features.Products.Mappings;

/// <summary>
/// Manual mapping extensions for the Product aggregate.
/// No AutoMapper — explicit projection per 03-CodingStandards.md.
/// </summary>
public static class ProductMappings
{
    // ── ProductImage ──────────────────────────────────────────────────────────

    public static ProductImageDto ToDto(this ProductImage image) =>
        new(image.Id, image.ImageUrl, image.DisplayOrder);

    // ── ProductListDto ────────────────────────────────────────────────────────

    /// <summary>
    /// Maps a Product (with optional Category navigation loaded) to the
    /// lightweight list DTO. Thumbnail = first non-deleted image by DisplayOrder.
    /// </summary>
    public static ProductListDto ToListDto(
        this Product product,
        string? categoryName = null,
        string? subCategoryName = null)
    {
        var thumbnail = product.Images
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => i.ImageUrl)
            .FirstOrDefault();

        return new ProductListDto(
            Id: product.Id,
            Name: product.Name,
            CategoryName: categoryName,
            SubCategoryName: subCategoryName,
            Barcode: product.Barcode,
            Status: product.Status,
            Price: product.Price,
            Currency: product.Currency,
            ThumbnailUrl: thumbnail,
            CreatedAt: product.CreatedAt
        );
    }

    // ── ProductDetailDto ──────────────────────────────────────────────────────

    /// <summary>
    /// Maps a fully-loaded Product (with Images and InventoryRecord) to the
    /// detail DTO. Pass null inventory parameters if no inventory record exists.
    /// </summary>
    public static ProductDetailDto ToDetailDto(
        this Product product,
        string? categoryName = null,
        string? subCategoryName = null,
        int? currentStock = null,
        string? inventoryStatus = null)
    {
        var images = product.Images
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => i.ToDto())
            .ToList()
            .AsReadOnly();

        return new ProductDetailDto(
            Id: product.Id,
            RetailerId: product.RetailerId,
            CategoryId: product.CategoryId,
            CategoryName: categoryName,
            SubCategoryId: product.SubCategoryId,
            SubCategoryName: subCategoryName,
            Name: product.Name,
            Description: product.Description,
            Price: product.Price,
            Currency: product.Currency,
            Barcode: product.Barcode,
            Status: product.Status,
            Images: images,
            CurrentStock: currentStock,
            InventoryStatus: inventoryStatus,
            CreatedAt: product.CreatedAt,
            UpdatedAt: product.UpdatedAt
        );
    }
}
