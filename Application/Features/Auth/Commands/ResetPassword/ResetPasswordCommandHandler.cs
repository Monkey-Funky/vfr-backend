using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Auth.Commands.ResetPassword;

/// <summary>
/// Handles password reset after OTP verification.
///
/// FLOW:
///   1. Look up the retailer by email
///   2. Retrieve the hashed OTP from Redis cache
///   3. Verify the client-supplied OTP against the stored hash (BCrypt.Verify)
///   4. Hash the new password (BCrypt, work factor 12)
///   5. Update the account password
///   6. Revoke all refresh tokens (forces re-login on all devices)
///   7. Delete the OTP from Redis (one-time use)
///   8. Persist and return success
/// </summary>
public sealed class ResetPasswordCommandHandler
    : IRequestHandler<ResetPasswordCommand, Result<bool>>
{
    private const string OtpCacheKeyPrefix = "pwd_reset:";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        // ── 1. Look up the retailer ───────────────────────────────────────────
        //
        // Only Active accounts are eligible for password reset.
        RetailerAccount? account = await _unitOfWork
            .Repository<RetailerAccount>()
            .FirstOrDefaultAsync(
                r => r.Email.ToLower() == command.Email.ToLower().Trim()
                  && !r.IsDeleted
                  && r.AccountStatus == RetailerAccount.Status.Active,
                cancellationToken);

        // Return a generic error — do not reveal whether the email exists.
        // This prevents enumeration of registered accounts.
        if (account is null)
        {
            _logger.LogWarning(
                "ResetPassword — email not found or account inactive.");

            throw new BusinessRuleException(
                "INVALID_OTP",
                "The OTP code is invalid or has expired. Please request a new one.");
        }

        // ── 2. Retrieve OTP hash from Redis ───────────────────────────────────
        //
        // A null result means the key either never existed or has expired (TTL elapsed).
        string cacheKey = $"{OtpCacheKeyPrefix}{account.Email.ToLowerInvariant()}";

        string? storedHashedOtp = await _cacheService.GetAsync<string>(
            cacheKey, cancellationToken);

        if (storedHashedOtp is null)
        {
            _logger.LogWarning(
                "ResetPassword — OTP not found in cache (likely expired). " +
                "RetailerId: {RetailerId}", account.Id);

            throw new BusinessRuleException(
                "INVALID_OTP",
                "The OTP code is invalid or has expired. Please request a new one.");
        }

        // ── 3. Verify OTP (BCrypt hash comparison) ────────────────────────────
        //
        // BCrypt.Verify is constant-time — safe against timing attacks on the OTP.
        bool otpValid = BCrypt.Net.BCrypt.Verify(command.OtpCode, storedHashedOtp);

        if (!otpValid)
        {
            _logger.LogWarning(
                "ResetPassword — OTP mismatch. RetailerId: {RetailerId}", account.Id);

            throw new BusinessRuleException(
                "INVALID_OTP",
                "The OTP code is incorrect. Please check your email and try again.");
        }

        // ── 4. Hash the new password ──────────────────────────────────────────
        //
        // BCrypt work factor 12 is mandatory per 07-SecurityArchitecture.md §3.1.
        string newPasswordHash = BCrypt.Net.BCrypt.HashPassword(
            command.NewPassword, workFactor: 12);

        // ── 5 & 6. Update password and revoke all tokens ──────────────────────
        //
        // ResetPassword sets the new password hash via the domain method (private setter).
        account.ResetPassword(newPasswordHash);

        // Revoking all refresh tokens forces every device to re-authenticate.
        // This is a security requirement on password change.
        account.RevokeAllRefreshTokens();

        // ── 7. Delete OTP from Redis (one-time use) ───────────────────────────
        //
        // Delete the OTP before the DB write to ensure it cannot be replayed
        // even if the subsequent SaveChangesAsync fails for a transient reason.
        await _cacheService.RemoveAsync(cacheKey, cancellationToken);

        // ── 8. Persist ────────────────────────────────────────────────────────
        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Password reset successful. RetailerId: {RetailerId}", account.Id);

        return Result<bool>.Success(true, "Password has been reset successfully. Please log in.");
    }
}