using Domain.Enums.Offer;
using Domain.Exceptions;

namespace Domain.Entities.Retailer;

/// <summary>
/// Represents a time-bound promotional offer created by a retailer.
///
/// DESIGN RULES:
///   • All property setters are private — mutations go through domain methods.
///   • A private parameterless constructor exists solely for EF Core materialisation.
///   • Mutual exclusivity is enforced by the DB CHECK constraint
///     and re-asserted in the factory method:
///       OfferType=Product  → ProductId IS NOT NULL, CategoryId IS NULL
///       OfferType=Category → CategoryId IS NOT NULL, ProductId IS NULL
///   • The Deactivate() method is the ONLY way to transition status to Expired.
///     It is called exclusively by OfferExpiryJob.
///   • Soft-delete is enforced via IsDeleted flag and the EF Core global query filter.
/// </summary>
public sealed class Offer : BaseEntity
{
    // =========================================================================
    // Properties
    // =========================================================================

    public Guid RetailerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string OfferType { get; private set; } = string.Empty;
    public Guid? ProductId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string DiscountType { get; private set; } = string.Empty;
    public decimal DiscountValue { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string CoverImageUrl { get; private set; } = string.Empty;
    public string Status { get; private set; } = OfferStatus.Active;

    // =========================================================================
    // Computed Properties
    // =========================================================================

    /// <summary>
    /// True when the offer has a defined end date that has already passed.
    /// Used by OfferExpiryJob to find candidates for Deactivate().
    /// </summary>
    public bool IsExpired =>
        EndDate.HasValue && EndDate.Value < DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Returns true when this offer is currently active and in-window.
    /// RULE: Status = Active AND StartDate &lt;= today AND (EndDate == null OR EndDate &gt;= today).
    /// Used by product-display layers to determine whether to show the discounted price.
    /// </summary>
    public bool IsActive(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        return Status == OfferStatus.Active
            && StartDate <= today
            && (!EndDate.HasValue || EndDate.Value >= today);
    }

    // =========================================================================
    // Navigation Properties (EF Core only — never use in Application layer)
    // =========================================================================

    public RetailerAccount Retailer { get; private set; } = null!;
    public Product? Product { get; private set; }
    public Category? Category { get; private set; }

    // =========================================================================
    // EF Core Constructor (private — never call directly)
    // =========================================================================

    private Offer() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates a new Offer entity ready to be persisted.
    /// Called exclusively from CreateOfferCommandHandler after all business-rule
    /// validation has passed (entity ownership, discount constraints).
    /// </summary>
    public static Offer Create(
        Guid retailerId,
        string title,
        string? description,
        string offerType,
        Guid? productId,
        Guid? categoryId,
        string discountType,
        decimal discountValue,
        DateOnly startDate,
        DateOnly? endDate,
        string coverImageUrl)
    {
        // Guard: retailerId
        if (retailerId == Guid.Empty)
            throw new ArgumentException("RetailerId must not be empty.", nameof(retailerId));

        // Guard: title
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));

        // Guard: offerType enum value — FIX O-1: use OfferType directly (Domain.Enums is imported)
        if (!Domain.Enums.Offer.OfferType.IsValid(offerType))
            throw new BusinessRuleException(
                "INVALID_OFFER_TYPE",
                $"OfferType must be one of: {string.Join(", ", Domain.Enums.Offer.OfferType.All)}.");

        // Guard: discountType enum value
        if (!Domain.Enums.Offer.DiscountType.IsValid(discountType))
            throw new BusinessRuleException(
                "INVALID_DISCOUNT_TYPE",
                $"DiscountType must be one of: {string.Join(", ", Domain.Enums.Offer.DiscountType.All)}.");

        // Guard: mutual exclusivity of ProductId / CategoryId
        if (offerType == Domain.Enums.Offer.OfferType.Product && (productId is null || productId == Guid.Empty))
            throw new BusinessRuleException(
                "PRODUCT_ID_REQUIRED",
                "ProductId is required for a Product-type offer.");

        if (offerType == Domain.Enums.Offer.OfferType.Category && (categoryId is null || categoryId == Guid.Empty))
            throw new BusinessRuleException(
                "CATEGORY_ID_REQUIRED",
                "CategoryId is required for a Category-type offer.");

        if (offerType == Domain.Enums.Offer.OfferType.Product && categoryId is not null)
            throw new BusinessRuleException(
                "MUTUAL_EXCLUSIVITY_VIOLATION",
                "A Product-type offer must not carry a CategoryId.");

        if (offerType == Domain.Enums.Offer.OfferType.Category && productId is not null)
            throw new BusinessRuleException(
                "MUTUAL_EXCLUSIVITY_VIOLATION",
                "A Category-type offer must not carry a ProductId.");

        // Guard: discountValue
        if (discountValue <= 0)
            throw new BusinessRuleException(
                "INVALID_DISCOUNT_VALUE",
                "DiscountValue must be greater than zero.");

        // Guard: coverImageUrl
        ArgumentException.ThrowIfNullOrWhiteSpace(coverImageUrl, nameof(coverImageUrl));

        return new Offer
        {
            Id = Guid.NewGuid(),
            RetailerId = retailerId,
            Title = title.Trim(),
            Description = description?.Trim(),
            OfferType = offerType,
            ProductId = offerType == Domain.Enums.Offer.OfferType.Product ? productId : null,
            CategoryId = offerType == Domain.Enums.Offer.OfferType.Category ? categoryId : null,
            DiscountType = discountType,
            DiscountValue = discountValue,
            StartDate = startDate,
            EndDate = endDate,
            CoverImageUrl = coverImageUrl,
            Status = OfferStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
    }

    // =========================================================================
    // Domain Methods
    // =========================================================================

    /// <summary>
    /// Transitions the offer status to <see cref="OfferStatus.Expired"/>.
    /// Called exclusively by <c>OfferExpiryJob</c> when the offer's EndDate has passed.
    /// </summary>
    public void Deactivate()
    {
        Status = OfferStatus.Expired;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Toggles the offer between Active and Inactive.
    /// Expired offers cannot be toggled — they must remain Expired.
    /// </summary>
    public void ToggleStatus()
    {
        if (Status == OfferStatus.Expired)
            throw new BusinessRuleException(
                "OFFER_EXPIRED",
                "An expired offer cannot be manually toggled. Create a new offer instead.");

        Status = Status == OfferStatus.Active ? OfferStatus.Inactive : OfferStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Updates offer metadata. Called from UpdateOfferCommandHandler.</summary>
    public void Update(
        string title,
        string? description,
        string discountType,
        decimal discountValue,
        DateOnly startDate,
        DateOnly? endDate,
        string status,
        string? newCoverImageUrl = null)
    {
        if (Status == OfferStatus.Expired)
            throw new BusinessRuleException(
                "OFFER_EXPIRED",
                "An expired offer cannot be updated.");

        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));

        // FIX O-1: use DiscountType directly (Domain.Enums is imported)
        if (!Domain.Enums.Offer.DiscountType.IsValid(discountType))
            throw new BusinessRuleException(
                "INVALID_DISCOUNT_TYPE",
                $"DiscountType must be one of: {string.Join(", ", DiscountType.All)}.");

        if (!OfferStatus.IsValid(status))
            throw new BusinessRuleException(
                "INVALID_STATUS",
                $"Status must be one of: {string.Join(", ", OfferStatus.All)}.");

        if (status == OfferStatus.Expired)
            throw new BusinessRuleException(
                "CANNOT_SET_EXPIRED",
                "Status cannot be manually set to Expired. The expiry job manages this transition.");

        if (discountValue <= 0)
            throw new BusinessRuleException(
                "INVALID_DISCOUNT_VALUE",
                "DiscountValue must be greater than zero.");

        Title = title.Trim();
        Description = description?.Trim();
        DiscountType = discountType;
        DiscountValue = discountValue;
        StartDate = startDate;
        EndDate = endDate;
        Status = status;
        UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(newCoverImageUrl))
            CoverImageUrl = newCoverImageUrl;
    }

    /// <summary>Soft-deletes the offer. Called from DeleteOfferCommandHandler.</summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}