namespace Domain.Events;
/// <summary>
/// Raised when a retailer requests account deletion via <c>DeleteAccountCommand</c>.
/// Triggers a GDPR-compliant "deletion confirmation" email to the retailer.
/// </summary>
/// <param name="RetailerId">The ID of the retailer who initiated the deletion.</param>
/// <param name="RetailerEmail">
///   The email at the moment of the request (before the purge job overwrites it).
/// </param>
/// <param name="OccurredAt">UTC timestamp of when the deletion was requested.</param>
public sealed record AccountDeletionRequestedEvent(
    Guid RetailerId,
    string RetailerEmail,
    DateTime OccurredAt) : IDomainEvent;