using Domain.Common;
using Domain.Exceptions;

namespace Domain.Entities.Retailer;

/// <summary>
/// Represents a saved payment method (card) belonging to a retailer.
/// Fields match B.13 of 02-DatabaseSchema.md.
///
/// Security Rules:
///   - CardholderName is ALWAYS stored AES-256 encrypted in the DB column
///     cardholder_name_encrypted. The entity stores the ciphertext only.
///   - CardNumberLast4 stores only the last 4 digits. Full PAN is never stored.
///   - StripePaymentMethodId holds the Stripe pm_xxxx token for charging.
///
/// At most one PaymentMethod per retailer may have IsDefault = true.
/// This invariant is enforced in SetDefaultPaymentMethodCommandHandler.
///
/// </summary>
public sealed class PaymentMethod : BaseEntity
{
    // ── Properties ────────────────────────────────────────────────────────────

    public Guid RetailerId { get; private set; }

    /// <summary>
    /// Card network / provider. Validated against the DB CHECK constraint.
    /// Valid values: Visa, Mastercard, PayPal, ApplePay, Stripe, GooglePay, Bitpay.
    /// </summary>
    public string ProviderType { get; private set; } = default!;

    /// <summary>
    /// Cardholder full name — stored AES-256 encrypted.
    /// Never read directly; always decrypted via IEncryptionService before returning.
    /// </summary>
    public string CardholderNameEncrypted { get; private set; } = default!;

    /// <summary>Last 4 digits of the card number. Never the full PAN.</summary>
    public string CardNumberLast4 { get; private set; } = default!;

    /// <summary>Card expiry in MM/YYYY format (e.g., "12/2027").</summary>
    public string ExpiryDate { get; private set; } = default!;

    /// <summary>
    /// Stripe payment method token (pm_xxxx). Used by StripePaymentGatewayService.ChargeAsync.
    /// Null when the card has not been tokenized via Stripe Elements.
    /// </summary>
    public string? StripePaymentMethodId { get; private set; }

    /// <summary>
    /// True when this is the retailer's designated recurring/default card.
    /// Only one card per retailer may be IsDefault = true.
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>True when the retailer opted to save this card for future use.</summary>
    public bool IsSaved { get; private set; }

    /// <summary>
    /// Card expiry date as a DateOnly for efficient DB expiry querying.
    /// Set to the last day of the expiry month (e.g., 12/2027 → 2027-12-31).
    ///
    /// </summary>
    public DateOnly ExpiresAt { get; private set; }

    // ── Computed Domain Properties ─────────────────────────────────────────────

    /// <summary>
    /// True when the card's expiry month/year has passed as of today UTC.
    /// Used as a guard in SelectPlan, UpgradePlan, and SetDefaultPaymentMethod handlers.
    /// </summary>
    public bool IsExpired => ExpiresAt < DateOnly.FromDateTime(DateTime.UtcNow);

    // ── Private Constructor (EF Core) ─────────────────────────────────────────

    private PaymentMethod() { }

    // ── Factory Method ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new PaymentMethod.
    /// <paramref name="cardholderNameEncrypted"/> must already be AES-256 encrypted
    /// by the caller using IEncryptionService.Encrypt() before passing here.
    ///
    /// </summary>
    public static PaymentMethod Create(
        Guid retailerId,
        string providerType,
        string cardholderNameEncrypted,
        string cardNumberLast4,
        string expiryDate,
        string? stripePaymentMethodId,
        bool isSaved = true)
    {
        if (retailerId == Guid.Empty)
            throw new BusinessRuleException(
                "PAYMENT_METHOD_RETAILER_REQUIRED", "RetailerId is required.");

        if (string.IsNullOrWhiteSpace(providerType))
            throw new BusinessRuleException(
                "PAYMENT_METHOD_PROVIDER_REQUIRED", "Provider type is required.");

        if (string.IsNullOrWhiteSpace(cardNumberLast4) || cardNumberLast4.Length != 4)
            throw new BusinessRuleException(
                "PAYMENT_METHOD_LAST4_INVALID",
                "CardNumberLast4 must be exactly 4 digits.");

        // BUG-007 FIX: Validates full MM/YYYY (month + year), not just year.
        if (!TryParseExpiryDate(expiryDate, out DateOnly expiresAt))
            throw new BusinessRuleException(
                "PAYMENT_METHOD_EXPIRY_INVALID",
                "ExpiryDate must be in MM/YYYY format (e.g., 12/2027) and must not be in the past.");

        // BUG-007 FIX: Guard against adding an already-expired card.
        if (expiresAt < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new BusinessRuleException(
                "PAYMENT_METHOD_ALREADY_EXPIRED",
                $"The card expiry '{expiryDate}' is in the past. Please use a valid card.");

        return new PaymentMethod
        {
            RetailerId = retailerId,
            ProviderType = providerType.Trim(),
            CardholderNameEncrypted = cardholderNameEncrypted,
            CardNumberLast4 = cardNumberLast4,
            ExpiryDate = expiryDate,
            StripePaymentMethodId = stripePaymentMethodId,
            IsDefault = false,
            IsSaved = isSaved,
            ExpiresAt = expiresAt
        };
    }

    // ── Domain Methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Marks this payment method as the retailer's default/recurring card.
    /// The caller is responsible for first un-setting IsDefault on all other cards.
    ///
    /// </summary>
    public void SetAsDefault()
    {
        if (IsExpired)
            throw new BusinessRuleException(
                "PAYMENT_METHOD_EXPIRED_DEFAULT",
                $"Card ending in {CardNumberLast4} expired on {ExpiryDate} and cannot be set as default. " +
                "Please add a valid card first.");

        IsDefault = true;
        SetUpdatedAudit(null, DateTime.UtcNow);
    }

    /// <summary>Removes the default flag from this payment method.</summary>
    public void UnsetDefault()
    {
        IsDefault = false;
        SetUpdatedAudit(null, DateTime.UtcNow);
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Parses "MM/YYYY" expiry date and computes the last day of the expiry month.
    /// Returns false if the format is invalid.
    ///
    
    /// </summary>
    private static bool TryParseExpiryDate(string expiryDate, out DateOnly expiresAt)
    {
        expiresAt = default;

        if (string.IsNullOrWhiteSpace(expiryDate))
            return false;

        string[] parts = expiryDate.Split('/');

        if (parts.Length != 2
            || !int.TryParse(parts[0], out int month)
            || !int.TryParse(parts[1], out int year)
            || month is < 1 or > 12)
        {
            return false;
        }

        int lastDay = DateTime.DaysInMonth(year, month);
        expiresAt = new DateOnly(year, month, lastDay);

        DateOnly currentMonthStart = new DateOnly(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1);

        // expiresAt must be >= first day of current month to be considered valid
        return expiresAt >= currentMonthStart;
    }
}