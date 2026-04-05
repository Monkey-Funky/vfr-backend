namespace Domain.Entities.Retailer;


/// <summary>
/// Represents a single image associated with a Product.
///
/// DESIGN RULES:
///   • All property setters are private.
///   • Private parameterless constructor exists only for EF Core.
///   • Does NOT extend BaseEntity because product_images has no
///     created_at, updated_at, created_by, or updated_by columns —
///     only id, product_id, image_url, display_order, is_deleted.
/// </summary>
public sealed class ProductImage
{
    // =========================================================================
    // Properties
    // =========================================================================

    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>FK to the parent Product.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Full public URL of the image in blob storage.</summary>
    public string ImageUrl { get; private set; } = string.Empty;

    /// <summary>Sort order (ascending). Lower value = displayed first.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Soft-delete flag.</summary>
    public bool IsDeleted { get; private set; }

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private ProductImage() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates a new ProductImage entity ready to be persisted.
    /// Called from Product.AddImage() — do not call from handlers directly.
    /// </summary>
    public static ProductImage Create(Guid productId, string imageUrl, int displayOrder)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId must not be empty.", nameof(productId));

        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl, nameof(imageUrl));

        return new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ImageUrl = imageUrl,
            DisplayOrder = displayOrder,
            IsDeleted = false
        };
    }

    // =========================================================================
    // Domain Methods
    // =========================================================================

    /// <summary>Marks this image as soft-deleted.</summary>
    public void SoftDelete() => IsDeleted = true;
}