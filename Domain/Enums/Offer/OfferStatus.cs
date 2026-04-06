namespace Domain.Enums.Offer;

/// <summary>
/// Lifecycle status values for an Offer.
/// Stored as varchar(20) in the database.
/// DB CHECK: status IN ('Active', 'Inactive', 'Expired').
///
/// Transition rules:
///   Active   → Inactive  : manual toggle by the retailer (ToggleOfferStatusCommand)
///   Inactive → Active    : manual toggle by the retailer (ToggleOfferStatusCommand)
///   Active   → Expired   : automatic via OfferExpiryJob calling Offer.Deactivate()
/// </summary>
public static class OfferStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Expired = "Expired";

    public static readonly IReadOnlyList<string> All = [Active, Inactive, Expired];

    public static bool IsValid(string value) => value is Active or Inactive or Expired;
}