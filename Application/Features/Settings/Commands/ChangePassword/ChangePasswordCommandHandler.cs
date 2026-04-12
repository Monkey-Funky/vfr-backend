using Application.Interfaces.Persistence;
using Application.Interfaces.Services;


namespace Application.Features.Settings.Commands.ChangePassword;

/// <summary>
/// Handles ChangePasswordCommand.
///
/// Steps:
///   1. Load the authenticated retailer's account.
///   2. Guard: OAuth-only accounts cannot set a password.
///   3. Verify the current password hash with BCrypt.
///   4. Hash the new password.
///   5. Call account.ChangePassword() which internally revokes all refresh tokens.
///   6. Persist via SaveChangesAsync.
///   7. Publish PasswordChangedEvent → SecurityAlertEmailHandler sends alert email.
/// </summary>
public sealed class ChangePasswordCommandHandler
    : IRequestHandler<ChangePasswordCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException(
                "Retailer identity could not be resolved from the access token.");

        // ── 1. Load the retailer account ──────────────────────────────────────
        RetailerAccount? account = await _unitOfWork
            .Repository<RetailerAccount>()
            .FirstOrDefaultAsync(
                r => r.Id == retailerId && !r.IsDeleted,
                cancellationToken);

        if (account is null)
            throw new NotFoundException(nameof(RetailerAccount), retailerId);

        // ── 2. Guard: Google-only accounts have no password ───────────────────
        if (string.IsNullOrEmpty(account.PasswordHash))
        {
            _logger.LogWarning(
                "ChangePassword — attempted on OAuth-only account. RetailerId: {RetailerId}",
                retailerId);

            throw new BusinessRuleException(
                "PASSWORD_NOT_SET",
                "This account was created with Google Sign-In and does not have a password. " +
                "Use the 'Set Password' flow instead.");
        }

        // ── 3. Verify current password via BCrypt ─────────────────────────────
        bool currentPasswordValid = BCrypt.Net.BCrypt.Verify(
            command.CurrentPassword,
            account.PasswordHash);

        if (!currentPasswordValid)
        {
            _logger.LogWarning(
                "ChangePassword — incorrect current password. RetailerId: {RetailerId}",
                retailerId);

            throw new BusinessRuleException(
                "INCORRECT_PASSWORD",
                "The current password you entered is incorrect.");
        }

        // ── 4. Hash the new password ──────────────────────────────────────────
        string newPasswordHash = BCrypt.Net.BCrypt.HashPassword(
            command.NewPassword, workFactor: 12);

        // ── 5. Change password + revoke ALL refresh tokens ────────────────────
        //
        // RetailerAccount.ChangePassword() internally calls RevokeAllRefreshTokens().
        // This nulls RefreshTokenHash and RefreshTokenExpiresAt, invalidating every
        // active session across all devices — not just the current one.
        account.ChangePassword(newPasswordHash);

        // ── 6. Persist ────────────────────────────────────────────────────────
        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "ChangePassword — password changed and all sessions revoked. RetailerId: {RetailerId}",
            retailerId);

        return Result<bool>.Success(
            true,
            "Password changed successfully. Please log in again on all your devices.");
    }
}