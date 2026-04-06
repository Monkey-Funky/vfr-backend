using Application.Interfaces.Persistence;
using Application.Interfaces.Services;


namespace Application.Features.Settings.Commands.DeleteAccount;

/// <summary>
/// Transitions the authenticated retailer's account into <c>PendingDeletion</c> state,
/// revokes all active sessions, and raises the <see cref="AccountDeletionRequestedEvent"/>
/// so that a confirmation email is dispatched asynchronously.
///
/// IDEMPOTENCY: If the account is already <c>PendingDeletion</c>, this handler returns
/// <c>Result.Success</c> immediately without performing any writes or raising a second event.
/// This guarantees that a client retrying the request (e.g. on a network timeout) never
/// sends the user a duplicate confirmation email or double-charges any side effects.
/// </summary>
public sealed class DeleteAccountCommandHandler
    : IRequestHandler<DeleteAccountCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublisher _publisher;
    private readonly ILogger<DeleteAccountCommandHandler> _logger;

    public DeleteAccountCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPublisher publisher,
        ILogger<DeleteAccountCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        DeleteAccountCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException(
                "Retailer identity could not be resolved from the access token.");

        // ── 1. Load account ───────────────────────────────────────────────────
        RetailerAccount? account = await _unitOfWork
            .Repository<RetailerAccount>()
            .FirstOrDefaultAsync(
                r => r.Id == retailerId && !r.IsDeleted,
                cancellationToken);

        if (account is null)
            throw new NotFoundException(nameof(RetailerAccount), retailerId);

        // ── 2. Idempotency guard ──────────────────────────────────────────────
        if (account.AccountStatus == RetailerAccount.Status.PendingDeletion)
        {
            _logger.LogInformation(
                "DeleteAccount — idempotent call, account already PendingDeletion. " +
                "RetailerId: {RetailerId}", retailerId);

            return Result<bool>.Success(
                true,
                "Account deletion is already pending. No further action is required.");
        }

        // ── 3. Capture email BEFORE status transition ─────────────────────────
        string retailerEmail = account.Email;

        // ── 4. Transition + revoke all sessions ───────────────────────────────
        account.MarkPendingDeletion();
        account.RevokeAllRefreshTokens();

        // ── 5. Persist ────────────────────────────────────────────────────────
        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── 6. Publish domain event AFTER successful save ─────────────────────
        // Published post-commit so the event is only raised when the state
        // transition is durable. Matches the IPublisher pattern used throughout
        // this codebase (see SubmitSaasEnquiryCommandHandler).
        await _publisher.Publish(
            new AccountDeletionRequestedEvent(
                RetailerId: retailerId,
                RetailerEmail: retailerEmail,
                OccurredAt: DateTime.UtcNow),
            cancellationToken);

        _logger.LogInformation(
            "DeleteAccount — account marked PendingDeletion and sessions revoked. " +
            "RetailerId: {RetailerId} | ScheduledPurgeAfter: 30 days",
            retailerId);

        return Result<bool>.Success(
            true,
            "Your account deletion request has been received. Your account and data will be " +
            "permanently deleted within 30 days. A confirmation email has been sent.");
    }
}