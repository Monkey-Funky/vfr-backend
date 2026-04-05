using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Mappings;


// ─────────────────────────────────────────────────────────────────────────────
// Product Image DTO
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Image read model embedded in ProductDetailDto.</summary>
public sealed record ProductImageDto(
    Guid Id,
    string ImageUrl,
    int DisplayOrder
);

// ─────────────────────────────────────────────────────────────────────────────
// List DTO  — lightweight record for paginated table views
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Compact product read model for the paginated products table.
/// Returned by GetProductsQuery.
/// </summary>
public sealed record ProductListDto(
    Guid Id,
    string Name,
    string? CategoryName,
    string? SubCategoryName,
    string? Barcode,
    string Status,
    decimal? Price,
    string Currency,
    string? ThumbnailUrl,   // first non-deleted image, null if no images
    DateTime CreatedAt
);

// ─────────────────────────────────────────────────────────────────────────────
// Detail DTO — full record for the product edit / detail page
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Full product read model including images and inventory summary.
/// Returned by GetProductByIdQuery.
/// </summary>
public sealed record ProductDetailDto(
    Guid Id,
    Guid RetailerId,
    Guid? CategoryId,
    string? CategoryName,
    Guid? SubCategoryId,
    string? SubCategoryName,
    string Name,
    string? Description,
    decimal? Price,
    string Currency,
    string? Barcode,
    string Status,
    IReadOnlyList<ProductImageDto> Images,
    // ── Inventory summary ──────────────────────────────────────────────────
    int? CurrentStock,
    string? InventoryStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);