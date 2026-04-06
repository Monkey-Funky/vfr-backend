
namespace Application.Features.Settings.Commands.DeleteAccount;

/// <summary>
/// Initiates a GDPR-compliant account deletion request.
///
/// This command is IDEMPOTENT:
///   • If the account is already in <c>PendingDeletion</c> status, it returns 200
///     immediately without raising a second event or performing any write.
///   • Only the first call transitions the account and raises
///     <see cref="AccountDeletionRequestedEvent"/>.
///
/// This command does NOT perform any PII purge. The actual data erasure is
/// handled by <c>AccountDeletionJob</c> (Infrastructure) after a 30-day grace period.
///
/// On success:
///   • Account status → PendingDeletion.
///   • All refresh tokens are revoked (all active sessions are immediately invalidated).
///   • <see cref="AccountDeletionRequestedEvent"/> is raised (triggers confirmation email).
/// </summary>
public sealed record DeleteAccountCommand : IRequest<Result<bool>>;