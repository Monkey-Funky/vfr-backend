namespace Domain.Entities.Customer;

/// <summary>
/// Represents a product that a customer has marked as a favorite.
/// 
/// DESIGN RULES:
///   • All property setters are private. State changes happen only through
///     the factory method (Create) or explicit domain methods.
///   • A partial unique index ensures a customer can only favorite a specific
///     product once (where is_deleted = false).
/// </summary>
public sealed class CustomerFavorite : BaseEntity
{
    // =========================================================================
    // Properties
    // =========================================================================

    /// <summary>FK to the owning customer. Never changes after construction.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>FK to the favorited product. Never changes after construction.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Denormalized FK to the retailer owning the product for easier filtering.</summary>
    public Guid RetailerId { get; private set; }

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private CustomerFavorite() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates a new CustomerFavorite entity.
    /// </summary>
    public static CustomerFavorite Create(Guid customerId, Guid productId, Guid retailerId)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId must not be empty.", nameof(customerId));

        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId must not be empty.", nameof(productId));
            
        if (retailerId == Guid.Empty)
            throw new ArgumentException("RetailerId must not be empty.", nameof(retailerId));

        return new CustomerFavorite
        {
            CustomerId = customerId,
            ProductId = productId,
            RetailerId = retailerId
        };
    }

    // =========================================================================
    // Domain Methods
    // =========================================================================

    /// <summary>
    /// Soft-deletes this favorite (e.g., when a user unfavorites a product).
    /// </summary>
    public void SoftDelete() => MarkAsDeleted();

    /// <summary>
    /// Restores a previously soft-deleted favorite.
    /// </summary>
    public void Restore() => IsDeleted = false;
}
