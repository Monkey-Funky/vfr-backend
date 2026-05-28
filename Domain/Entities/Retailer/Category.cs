namespace Domain.Entities.Retailer;

// src/Domain/Entities/Retailer/Category.cs

/// <summary>
/// Aggregate root representing a product category owned by a specific retailer.
///
/// DESIGN RULES:
///   • All property setters are private. State changes happen only through the
///     factory method (Create) or explicit domain methods.
///   • The private parameterless constructor exists only for EF Core.
///   • SubCategories is a navigation collection — EF Core populates it via Include.
///     The backing list is private; outside callers receive a read-only view.
///   • Depth constraint: max depth = 1. A Category can have SubCategories but a
///     SubCategory cannot have its own SubCategories. Enforced in CreateSubCategoryCommandHandler.
/// </summary>
public sealed class Category : BaseEntity
{
    // =========================================================================
    // Status Constants
    // =========================================================================

    /// <summary>All valid lifecycle status values for a Category.</summary>
    public static class CategoryStatus
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
    }

    // =========================================================================
    // Properties
    // =========================================================================

    /// <summary>FK to the owning retailer. Never null after construction.</summary>
    public Guid RetailerId { get; private set; }

    /// <summary>
    /// Display name of the category. Max 150 chars.
    /// Partial unique index: (retailer_id, name) WHERE is_deleted = false.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Optional free-text description.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Full public URL of the cover image in blob storage.
    /// Required at creation time. Replaced (old image deleted from S3) on update.
    /// </summary>
    public string CoverImageUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Lifecycle status. Use <see cref="CategoryStatus"/> constants.
    /// Defaults to Active on creation.
    /// </summary>
    public string Status { get; private set; } = CategoryStatus.Active;

    // =========================================================================
    // Navigation
    // =========================================================================

    private readonly List<SubCategory> _subCategories = [];

    /// <summary>
    /// Child sub-categories. EF Core populates this via Include.
    /// Use IReadOnlyCollection to prevent external callers from mutating the collection.
    /// </summary>
    public IReadOnlyCollection<SubCategory> SubCategories => _subCategories.AsReadOnly();

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private Category() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates a new Category entity ready to be persisted.
    /// </summary>
    /// <param name="retailerId">The owning retailer's ID. Must not be empty.</param>
    /// <param name="name">Category display name. Max 150 chars. Must not be blank.</param>
    /// <param name="description">Optional description. Null is allowed.</param>
    /// <param name="coverImageUrl">Full public URL of the cover image. Must not be blank.</param>
    /// <param name="status">Initial status. Use <see cref="CategoryStatus"/> constants.</param>
    public static Category Create(
        Guid retailerId,
        string name,
        string? description,
        string coverImageUrl,
        string status)
    {
        if (retailerId == Guid.Empty)
            throw new ArgumentException("RetailerId must not be empty.", nameof(retailerId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(coverImageUrl, nameof(coverImageUrl));
        ArgumentException.ThrowIfNullOrWhiteSpace(status, nameof(status));

        return new Category
        {
            RetailerId = retailerId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CoverImageUrl = coverImageUrl,
            Status = status,
        };
    }

    // =========================================================================
    // Domain Methods
    // =========================================================================

    /// <summary>
    /// Updates mutable fields. Null arguments are treated as "no change" for
    /// non-nullable fields (Name, Status, CoverImageUrl). Description is always
    /// replaced: pass null to clear it, pass a string to set it.
    /// </summary>
    /// <param name="newName">New name value. Null = keep existing.</param>
    /// <param name="newDescription">New description. Null = clear existing.</param>
    /// <param name="shouldUpdateDescription">
    ///   Must be true for <paramref name="newDescription"/> to be applied.
    ///   This flag distinguishes "caller did not include description" from "caller
    ///   explicitly wants to clear it".
    /// </param>
    /// <param name="newCoverImageUrl">New cover image URL. Null = keep existing.</param>
    /// <param name="newStatus">New status value. Null = keep existing.</param>
    public void Update(
        string? newName,
        string? newDescription,
        bool shouldUpdateDescription,
        string? newCoverImageUrl,
        string? newStatus)
    {
        if (newName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(newName, nameof(newName));
            Name = newName.Trim();
        }

        if (shouldUpdateDescription)
            Description = newDescription?.Trim();

        if (newCoverImageUrl is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(newCoverImageUrl, nameof(newCoverImageUrl));
            CoverImageUrl = newCoverImageUrl;
        }

        if (newStatus is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(newStatus, nameof(newStatus));
            Status = newStatus;
        }
    }

    /// <summary>
    /// Toggles Status between Active and Inactive.
    /// </summary>
    /// <returns>The new status string after the toggle.</returns>
    public string ToggleStatus()
    {
        Status = Status == CategoryStatus.Active
            ? CategoryStatus.Inactive
            : CategoryStatus.Active;

        return Status;
    }

    public void AddSubCategory(SubCategory subCategory)
    {
        ArgumentNullException.ThrowIfNull(subCategory);

        var isDuplicate = _subCategories.Any(s =>
            !s.IsDeleted &&
            string.Equals(s.Name, subCategory.Name, StringComparison.OrdinalIgnoreCase));

        if (isDuplicate)
            throw new Domain.Exceptions.BusinessRuleException(
                "DUPLICATE_SUBCATEGORY_NAME",
                $"A sub-category named '{subCategory.Name}' already exists in this category.");

        _subCategories.Add(subCategory);
    }
}