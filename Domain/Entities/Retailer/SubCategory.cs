namespace Domain.Entities.Retailer;


/// <summary>
/// Represents a sub-category belonging to exactly one parent Category.
///
/// DEPTH CONSTRAINT: Max depth = 1. A SubCategory cannot have its own
/// SubCategories. This is enforced in <c>CreateSubCategoryCommandHandler</c>
/// by verifying that the ParentCategoryId refers to a Category (not a SubCategory).
///
/// DESIGN RULES:
///   • All property setters are private.
///   • Private parameterless constructor exists only for EF Core.
///   • No CreatedBy / UpdatedBy columns (not in the DB schema for this entity).
///     The EF configuration ignores those inherited BaseEntity properties.
/// </summary>
public sealed class SubCategory : BaseEntity
{
    // =========================================================================
    // Status Constants
    // =========================================================================

    /// <summary>All valid lifecycle status values for a SubCategory.</summary>
    public static class SubCategoryStatus
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
    }

    // =========================================================================
    // Properties
    // =========================================================================

    /// <summary>FK to the parent Category. Never null after construction.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>
    /// FK to the owning retailer. Denormalized for efficient tenant-scoped queries
    /// without joining back to the parent category.
    /// </summary>
    public Guid RetailerId { get; private set; }

    /// <summary>
    /// Display name of the sub-category. Max 150 chars.
    /// Partial unique index: (category_id, name) WHERE is_deleted = false.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Lifecycle status. Use <see cref="SubCategoryStatus"/> constants.</summary>
    public string Status { get; private set; } = SubCategoryStatus.Active;

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private SubCategory() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates a new SubCategory entity ready to be persisted.
    /// </summary>
    /// <param name="categoryId">Parent category ID. Must not be empty.</param>
    /// <param name="retailerId">The owning retailer's ID. Must not be empty.</param>
    /// <param name="name">Sub-category name. Max 150 chars. Must not be blank.</param>
    /// <param name="status">Initial status. Use <see cref="SubCategoryStatus"/> constants.</param>
    public static SubCategory Create(
        Guid categoryId,
        Guid retailerId,
        string name,
        string status)
    {
        if (categoryId == Guid.Empty)
            throw new ArgumentException("CategoryId must not be empty.", nameof(categoryId));

        if (retailerId == Guid.Empty)
            throw new ArgumentException("RetailerId must not be empty.", nameof(retailerId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(status, nameof(status));

        return new SubCategory
        {
            CategoryId = categoryId,
            RetailerId = retailerId,
            Name = name.Trim(),
            Status = status,
        };
    }

    // =========================================================================
    // Domain Methods
    // =========================================================================

    /// <summary>
    /// Updates mutable fields. Null arguments are treated as "no change".
    /// </summary>
    /// <param name="newName">New name. Null = keep existing.</param>
    /// <param name="newStatus">New status. Null = keep existing.</param>
    public void Update(string? newName, string? newStatus)
    {
        if (newName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(newName, nameof(newName));
            Name = newName.Trim();
        }

        if (newStatus is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(newStatus, nameof(newStatus));
            Status = newStatus;
        }
    }
}