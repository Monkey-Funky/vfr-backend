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

    /// <summary>FK to the owning retailer. Never null after construction.</summary>
    public Guid RetailerId { get; private set; }

    /// <summary>Display title shown to customers. Max 200 chars (varchar(200) in DB).</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Optional marketing description. Mapped to text in DB.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Determines whether the offer targets a Product or a Category.
    /// See <see cref="OfferType"/> for valid values.
    /// </summary>
    public string OfferType { get; private set; } = string.Empty;

    /// <summary>
    /// FK to the targeted product. Non-null when OfferType = Product.
    /// Null when OfferType = Category.
    /// ON DELETE SET NULL in the DB — if the product is deleted, this is set to null
    /// but the offer row is retained.
    /// </summary>
    public Guid? ProductId { get; private set; }

    /// <summary>
    /// FK to the targeted category. Non-null when OfferType = Category.
    /// Null when OfferType = Product.
    /// ON DELETE SET NULL in the DB.
    /// </summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>
    /// Determines how DiscountValue is interpreted.
    /// See <see cref="DiscountType"/> for valid values.
    /// </summary>
    public string DiscountType { get; private set; } = string.Empty;

    /// <summary>
    /// The discount amount or percentage.
    ///   Percentage: 1–100 (inclusive)
    ///   Fixed: > 0 and ≤ product price (validated in CreateOfferCommandHandler)
    /// Stored as numeric(18,2) in the DB.
    /// </summary>
    public decimal DiscountValue { get; private set; }

    /// <summary>Date on which the offer becomes active. Mapped to PostgreSQL date.</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>
    /// Optional expiry date. Null = open-ended offer.
    /// When EndDate < today and Status = Active, OfferExpiryJob calls Deactivate().
    /// </summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>Public-facing cover image URL. Uploaded via IFileStorageService.</summary>
    public string CoverImageUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Current lifecycle status.
    /// See <see cref="OfferStatus"/> for valid values and transition rules.
    /// </summary>
    public string Status { get; private set; } = OfferStatus.Active;


    // =========================================================================
    // Computed Properties
    // =========================================================================

    /// <summary>
    /// True when the offer has a defined end date that has already passed.
    /// Used by OfferExpiryJob to identify candidates for Deactivate().
    /// </summary>
    public bool IsExpired =>
        EndDate.HasValue && EndDate.Value < DateOnly.FromDateTime(DateTime.UtcNow);

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
    /// validation has passed (entity ownership, discount constraints, plan limits).
    /// </summary>
    /// <param name="retailerId">Owning retailer. Must not be Guid.Empty.</param>
    /// <param name="title">Offer display title. Max 200 chars.</param>
    /// <param name="description">Optional marketing description.</param>
    /// <param name="offerType">See <see cref="OfferType"/>. Must be a valid value.</param>
    /// <param name="productId">Required when offerType = Product; null otherwise.</param>
    /// <param name="categoryId">Required when offerType = Category; null otherwise.</param>
    /// <param name="discountType">See <see cref="DiscountType"/>. Must be a valid value.</param>
    /// <param name="discountValue">Must be > 0 and within type-specific range.</param>
    /// <param name="startDate">Must not be in the past.</param>
    /// <param name="endDate">Optional expiry date. Must be after startDate when supplied.</param>
    /// <param name="coverImageUrl">Blob storage URL returned by IFileStorageService.</param>
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

        // Guard: offerType enum value
        if (!Enums.Offer.OfferType.IsValid(offerType))
            throw new BusinessRuleException(
                "INVALID_OFFER_TYPE",
                $"OfferType must be one of: {string.Join(", ", Enums.Offer.OfferType.All)}.");

        // Guard: discountType enum value
        if (!Enums.Offer.DiscountType.IsValid(discountType))
            throw new BusinessRuleException(
                "INVALID_DISCOUNT_TYPE",
                $"DiscountType must be one of: {string.Join(", ", Enums.Offer.DiscountType.All)}.");

        // Guard: mutual exclusivity of ProductId / CategoryId
        if (offerType == Enums.Offer.OfferType.Product && (productId is null || productId == Guid.Empty))
            throw new BusinessRuleException(
                "PRODUCT_ID_REQUIRED",
                "ProductId is required for a Product-type offer.");

        if (offerType == Enums.Offer.OfferType.Category && (categoryId is null || categoryId == Guid.Empty))
            throw new BusinessRuleException(
                "CATEGORY_ID_REQUIRED",
                "CategoryId is required for a Category-type offer.");

        if (offerType == Enums.Offer.OfferType.Product && categoryId is not null)
            throw new BusinessRuleException(
                "MUTUAL_EXCLUSIVITY_VIOLATION",
                "A Product-type offer must not carry a CategoryId.");

        if (offerType == Enums.Offer.OfferType.Category && productId is not null)
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
            ProductId = offerType == Enums.Offer.OfferType.Product ? productId : null,
            CategoryId = offerType == Enums.Offer.OfferType.Category ? categoryId : null,
            DiscountType = discountType,
            DiscountValue = discountValue,
            StartDate = startDate,
            EndDate = endDate,
            CoverImageUrl = coverImageUrl,
            Status = OfferStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    // =========================================================================
    // Domain Methods
    // =========================================================================

    /// <summary>
    /// Transitions the offer status to <see cref="OfferStatus.Expired"/>.
    /// Called exclusively by <c>OfferExpiryJob</c> when the offer's EndDate
    /// has passed. Not to be confused with manual deactivation via
    /// <see cref="ToggleStatus"/>, which cycles between Active/Inactive.
    /// </summary>
    public void Deactivate()
    {
        Status = OfferStatus.Expired;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Toggles the offer between Active and Inactive.
    /// Expired offers cannot be toggled — an Expired offer must remain Expired.
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

        if (!Enums.Offer.DiscountType.IsValid(discountType))
            throw new BusinessRuleException(
                "INVALID_DISCOUNT_TYPE",
                $"DiscountType must be one of: {string.Join(", ", Enums.Offer.DiscountType.All)}.");

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