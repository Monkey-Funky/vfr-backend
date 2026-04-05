
namespace Domain.Entities.Retailer;

/// <summary>
/// Represents a promotional offer created by a retailer.
///
/// STUB IMPLEMENTATION — P-022 will complete all properties, factory methods,
/// validators, and handlers. Only the fields used by the Category cascade
/// (RetailerId, CategoryId, Status, Deactivate()) are present here.
/// </summary>
public sealed class Offer : BaseEntity
{
    // =========================================================================
    // Status Constants (subset used by cascade logic)
    // =========================================================================

    /// <summary>All valid lifecycle status values for an Offer.</summary>
    public static class OfferStatus
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
        public const string Expired = "Expired";
    }

    // =========================================================================
    // Properties (stub — P-022 adds remaining columns)
    // =========================================================================

    public Guid RetailerId { get; private set; }

    /// <summary>
    /// FK to the parent Category. Nullable — offers may be product-scoped or category-scoped.
    /// Set to null via DB ON DELETE SET NULL when the category is hard-deleted.
    /// Set to Inactive programmatically when the category is soft-deleted.
    /// </summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>Lifecycle status. Use <see cref="OfferStatus"/> constants.</summary>
    public string Status { get; private set; } = OfferStatus.Active;

    // =========================================================================
    // EF Core Constructor
    // =========================================================================

    private Offer() { }

    // =========================================================================
    // Domain Methods (cascade support)
    // =========================================================================

    /// <summary>
    /// Sets this offer's status to Inactive.
    /// Called by <c>DeleteCategoryCommandHandler</c> during the cascade operation.
    /// </summary>
    public void Deactivate()
    {
        Status = OfferStatus.Inactive;
    }
}