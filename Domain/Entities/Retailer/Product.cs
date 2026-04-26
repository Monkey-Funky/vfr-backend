using Domain.Enums.Product;
using Domain.Exceptions;

namespace Domain.Entities.Retailer;

/// <summary>
/// Aggregate root representing a product owned by a specific retailer.
///
/// DESIGN RULES:
///   • All property setters are private. State changes happen only through
///     the factory method (Create) or explicit domain methods.
///   • ProductImages is a navigation collection populated by EF Core via Include.
///     The backing list is private; outside callers receive a read-only view.
///   • Product does NOT own InventoryRecord directly — that is a separate
///     aggregate root created atomically alongside Product in the handler.
///   • search_vector is a GENERATED ALWAYS AS ... STORED column in PostgreSQL.
///     EF Core is told to ignore writes to it (ValueGeneratedOnAddOrUpdate).
/// </summary>
public sealed class Product : BaseEntity
{
    // =========================================================================
    // Properties
    // =========================================================================

    /// <summary>FK to the owning retailer. Never changes after construction.</summary>
    public Guid RetailerId { get; private set; }

    /// <summary>
    /// Optional FK to a Category. Nullable — a product may be uncategorised.
    /// DB: SET NULL on category deletion.
    /// </summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>
    /// Optional FK to a SubCategory. Must belong to CategoryId when set.
    /// Validated by handlers before calling UpdateCategory().
    /// DB: SET NULL on sub-category deletion.
    /// </summary>
    public Guid? SubCategoryId { get; private set; }

    /// <summary>
    /// Display name. Max 200 chars.
    /// Partial UNIQUE index: (retailer_id, name) WHERE is_deleted = false.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Optional free-text description. Max 2000 chars. Stored as TEXT in PostgreSQL.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Unit price. Null when the product has not been priced yet.
    /// Always > 0 when set — enforced in Create() and Update().
    /// </summary>
    public decimal? Price { get; private set; }

    /// <summary>ISO 4217 currency code, e.g. "EGP". Defaults to "EGP".</summary>
    public string Currency { get; private set; } = "EGP";

    /// <summary>Optional barcode string. Max 100 chars. Must be alphanumeric (validated by FluentValidation).</summary>
    public string? Barcode { get; private set; }

    /// <summary>
    /// Lifecycle status. Use <see cref="ProductStatus"/> constants — never raw strings.
    /// Valid values: Active | Inactive | Draft.
    /// Enforced by DB CHECK constraint ck_products_status.
    /// </summary>
    public string Status { get; private set; } = ProductStatus.Draft;

    // =========================================================================
    // Navigation — Images
    // =========================================================================

    // Private backing list: EF Core populates this via Include(p => p.Images).
    // Domain methods AddImage / RemoveImage are the only permitted mutation paths.
    private readonly List<ProductImage> _images = [];

    /// <summary>
    /// Read-only view of the product's images.
    /// Ordered by DisplayOrder in query projections — not guaranteed here in memory.
    /// Soft-deleted images are NOT filtered here; filter at the query/mapping layer.
    /// </summary>
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    // Required by EF Core to materialise entities from database rows.
    // The parameterless constructor does not initialise the _images backing field
    // because EF Core populates it via the shadow navigation after construction.
    private Product() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates a new Product entity ready to be persisted.
    ///
    /// Does NOT create the InventoryRecord — that is done atomically alongside
    /// this entity inside CreateProductCommandHandler.ExecuteInTransactionAsync.
    ///
    /// Does NOT upload images — S3 upload happens before calling this method;
    /// image URLs are attached afterward via AddImage().
    /// </summary>
    /// <param name="retailerId">Owning retailer. Must not be Guid.Empty.</param>
    /// <param name="name">Display name. Required. Trimmed. Max 200 chars.</param>
    /// <param name="description">Optional description. Trimmed if provided.</param>
    /// <param name="categoryId">Optional FK to a Category owned by this retailer.</param>
    /// <param name="subCategoryId">Optional FK to a SubCategory. Requires categoryId.</param>
    /// <param name="price">Optional price. Must be > 0 when provided.</param>
    /// <param name="currency">ISO currency code. Defaults to "EGP".</param>
    /// <param name="barcode">Optional barcode. Trimmed if provided.</param>
    /// <param name="status">Lifecycle status. Must be a valid ProductStatus value.</param>
    public static Product Create(
        Guid retailerId,
        string name,
        string? description = null,
        Guid? categoryId = null,
        Guid? subCategoryId = null,
        decimal? price = null,
        string currency = "EGP",
        string? barcode = null,
        string status = ProductStatus.Draft)
    {
        if (retailerId == Guid.Empty)
            throw new ArgumentException(
                "RetailerId must not be empty.", nameof(retailerId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency, nameof(currency));
        ArgumentException.ThrowIfNullOrWhiteSpace(status, nameof(status));

        if (!ProductStatus.IsValid(status))
            throw new BusinessRuleException(
                "INVALID_PRODUCT_STATUS",
                $"'{status}' is not a valid product status. " +
                $"Allowed: {string.Join(", ", ProductStatus.All)}.");

        if (price.HasValue && price.Value <= 0)
            throw new BusinessRuleException(
                "INVALID_PRODUCT_PRICE",
                "Price must be greater than zero when provided.");

        return new Product
        {
            // Id and CreatedAt are set by BaseEntity's constructor
            RetailerId = retailerId,
            CategoryId = categoryId,
            SubCategoryId = subCategoryId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Price = price,
            Currency = currency.Trim().ToUpperInvariant(),
            Barcode = barcode?.Trim(),
            Status = status
        };
    }

    // =========================================================================
    // Domain Methods — Images
    // =========================================================================

    /// <summary>
    /// Creates a new ProductImage and adds it to this product's image collection.
    ///
    /// Called by:
    ///   • CreateProductCommandHandler — after all S3 uploads succeed, inside
    ///     ExecuteInTransactionAsync, to attach image URLs to the new product.
    ///   • AddProductImageCommandHandler — after the single S3 upload, before
    ///     SaveChangesAsync.
    /// </summary>
    /// <param name="imageUrl">Full public S3 URL. Must not be null or whitespace.</param>
    /// <param name="displayOrder">Sort position. Lower = displayed first.</param>
    public ProductImage AddImage(string imageUrl, int displayOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl, nameof(imageUrl));

        if (displayOrder < 0)
            throw new BusinessRuleException(
                "INVALID_DISPLAY_ORDER",
                "Display order cannot be negative.");

        var image = ProductImage.Create(Id, imageUrl, displayOrder);
        _images.Add(image);
        
        return image;
    }

    /// <summary>
    /// Soft-deletes an image by its ID.
    ///
    /// Called by RemoveProductImageCommandHandler.
    /// Throws <see cref="NotFoundException"/> if the image does not exist
    /// in this product's collection or is already soft-deleted.
    /// </summary>
    /// <param name="imageId">ID of the image to remove.</param>
    public void RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId && !i.IsDeleted)
            ?? throw new NotFoundException(nameof(ProductImage), imageId);

        image.SoftDelete();
    }

    // =========================================================================
    // Domain Methods — Status
    // =========================================================================

    /// <summary>
    /// Cycles the product's status:
    ///   Active   → Inactive
    ///   Inactive → Active
    ///   Draft    → Active  (first activation)
    ///
    /// Called by ToggleProductStatusCommandHandler.
    /// </summary>
    /// <returns>The new status string after toggling.</returns>
    public string ToggleStatus()
    {
        Status = Status == ProductStatus.Active
            ? ProductStatus.Inactive
            : ProductStatus.Active;

        return Status;
    }

    /// <summary>
    /// Explicitly sets the lifecycle status.
    /// Validates the new value against <see cref="ProductStatus.IsValid"/>.
    ///
    /// Called by Product.Update() when NewStatus is provided.
    /// </summary>
    /// <param name="newStatus">Target status. Must be a valid ProductStatus constant.</param>
    public void ChangeStatus(string newStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newStatus, nameof(newStatus));

        if (!ProductStatus.IsValid(newStatus))
            throw new BusinessRuleException(
                "INVALID_PRODUCT_STATUS",
                $"'{newStatus}' is not a valid product status. " +
                $"Allowed: {string.Join(", ", ProductStatus.All)}.");

        Status = newStatus;
    }

    // =========================================================================
    // Domain Methods — Category
    // =========================================================================

    /// <summary>
    /// Updates the category and sub-category assignment atomically.
    ///
    /// SubCategoryId cross-category validation (SubCategory must belong to CategoryId)
    /// is performed by the handler BEFORE calling this method — this method
    /// trusts that the handler has already verified the relationship.
    ///
    /// Pass null for both to clear all category assignments.
    ///
    /// Called by Product.Update() when ShouldUpdateCategory is true.
    /// </summary>
    /// <param name="categoryId">New CategoryId, or null to clear.</param>
    /// <param name="subCategoryId">New SubCategoryId, or null to clear.</param>
    public void UpdateCategory(Guid? categoryId, Guid? subCategoryId)
    {
        // Business rule: SubCategoryId requires a parent CategoryId
        if (subCategoryId.HasValue && !categoryId.HasValue)
            throw new BusinessRuleException(
                "SUB_CATEGORY_WITHOUT_CATEGORY",
                "SubCategoryId cannot be set without a CategoryId.");

        CategoryId = categoryId;
        SubCategoryId = subCategoryId;
    }

    // =========================================================================
    // Domain Methods — Full Update (UpdateProductCommand)
    // =========================================================================

    /// <summary>
    /// Applies a partial or full update to this product's mutable fields.
    ///
    /// The ShouldUpdate* flags distinguish "do not change" from "explicitly clear":
    ///   ShouldUpdateDescription = false → leave Description unchanged
    ///   ShouldUpdateDescription = true, NewDescription = null → clear Description to null
    ///
    /// Category cross-validation (SubCategory belongs to Category, Category belongs
    /// to this retailer) is performed by UpdateProductCommandHandler BEFORE calling
    /// this method.
    ///
    /// Called by UpdateProductCommandHandler.
    /// </summary>
    public void Update(
        string? newName,
        string? newDescription,
        bool shouldUpdateDescription,
        decimal? newPrice,
        bool shouldUpdatePrice,
        string? newBarcode,
        bool shouldUpdateBarcode,
        Guid? newCategoryId,
        bool shouldUpdateCategory,
        Guid? newSubCategoryId,
        string? newStatus)
    {
        // Name: only update when a non-null value is provided
        if (newName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(newName, nameof(newName));
            Name = newName.Trim();
        }

        // Description: flag controls whether to overwrite (even with null)
        if (shouldUpdateDescription)
            Description = newDescription?.Trim();

        // Price: flag controls whether to overwrite; validate if a value is given
        if (shouldUpdatePrice)
        {
            if (newPrice.HasValue && newPrice.Value <= 0)
                throw new BusinessRuleException(
                    "INVALID_PRODUCT_PRICE",
                    "Price must be greater than zero when provided.");

            Price = newPrice;
        }

        // Barcode: flag controls whether to overwrite (even with null to clear)
        if (shouldUpdateBarcode)
            Barcode = newBarcode?.Trim();

        // Category + SubCategory: updated atomically via domain method
        if (shouldUpdateCategory)
            UpdateCategory(newCategoryId, newSubCategoryId);

        // Status: only update when a new value is explicitly provided
        if (newStatus is not null)
            ChangeStatus(newStatus);
    }

    // =========================================================================
    // Domain Methods — Soft Delete
    // =========================================================================

    /// <summary>
    /// Marks this product as soft-deleted.
    ///
    /// Called by DeleteProductCommandHandler inside ExecuteInTransactionAsync,
    /// alongside InventoryRecord.SoftDelete(), so both are persisted atomically.
    ///
    /// Delegates to BaseEntity.MarkAsDeleted() so the IsDeleted flag is set
    /// through the protected setter without needing to break encapsulation.
    /// </summary>
    public void SoftDelete() => MarkAsDeleted();
}