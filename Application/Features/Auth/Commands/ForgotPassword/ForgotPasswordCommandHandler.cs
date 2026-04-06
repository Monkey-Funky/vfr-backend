
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using System.Security.Cryptography;

namespace Application.Features.Auth.Commands.ForgotPassword;

/// <summary>
/// Handles the forgot-password request.
///
/// FLOW:
///   1. Look up the retailer by email (case-insensitive)
///   2. If not found: return success immediately (prevents email enumeration)
///   3. If found: generate a 6-digit OTP, hash it with BCrypt, cache it in Redis
///      with a 15-minute TTL using key "pwd_reset:{email}"
///   4. Send the OTP to the retailer's email address
///   5. Return success regardless of any email-existence result
///
/// SECURITY:
///   The OTP is hashed before being stored in Redis.
///   The raw OTP is only sent to the email address — it is never logged.
/// </summary>
public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result<bool>>
{
    // Cache key prefix for password reset OTPs stored in Redis.
    private const string OtpCacheKeyPrefix = "pwd_reset:";

    // OTP TTL matches the 15-minute security requirement.
    private static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(15);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IEmailService emailService,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken)
    {
        // ── 1. Look up the retailer ───────────────────────────────────────────
        //
        // Only Active accounts can reset their password. PendingEmailVerification,
        // Suspended, and PendingDeletion accounts are not eligible.
        RetailerAccount? account = await _unitOfWork
            .Repository<RetailerAccount>()
            .FirstOrDefaultAsync(
                r => r.Email.ToLower() == command.Email.ToLower().Trim()
                  && !r.IsDeleted
                  && r.AccountStatus == RetailerAccount.Status.Active,
                cancellationToken);

        // ── 2. Email not found — constant-time path ───────────────────────────
        //
        // Run a dummy BCrypt hash at work factor 4 (≈ 20ms) to equalise response
        // time with the found-email path (BCrypt at work factor 12 ≈ 300ms for the OTP).
        // The two paths will not be perfectly equal, but the dummy work closes the
        // most easily measurable timing gap (zero work vs. hundreds of milliseconds).
        if (account is null)
        {
            _ = BCrypt.Net.BCrypt.HashPassword(command.Email, workFactor: 4);

            _logger.LogInformation(
                "ForgotPassword — email not found or account inactive, returning silent success.");

            return Result<bool>.Success(
                true,
                "If an account with that email exists, a reset code has been sent.");
        }

        // ── 3. Generate a 6-digit CSPRNG OTP ─────────────────────────────────
        //
        // FIX F-05: Replace Random.Shared.Next() (PRNG) with RandomNumberGenerator.GetInt32()
        // (CSPRNG). This is cryptographically secure and resistant to prediction attacks.
        //
        // Range: [100_000, 1_000_000) — all 900_000 six-digit codes are reachable.
        // The previous code used Next(100_000, 999_999) which excluded 999_999.
        string rawOtp = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();

        string hashedOtp = BCrypt.Net.BCrypt.HashPassword(rawOtp, workFactor: 12);

        string cacheKey = $"{OtpCacheKeyPrefix}{account.Email.ToLowerInvariant()}";
        await _cacheService.SetAsync(cacheKey, hashedOtp, OtpTtl, cancellationToken);

        _logger.LogInformation(
            "Password reset OTP cached. RetailerId: {RetailerId}. Expiry: {ExpiryMinutes} min.",
            account.Id, OtpTtl.TotalMinutes);

        // ── 4. Send OTP email ─────────────────────────────────────────────────
        //
        // OTP email is CRITICAL (not fire-and-forget) — the user cannot reset their
        // password if this email doesn't arrive. Awaited here.
        await _emailService.SendEmailAsync(
            to: account.Email,
            subject: "Your VFR Password Reset Code",
            body: $"<p>Hi {account.FullName},</p>" +
                     $"<p>Your password reset code is: <strong>{rawOtp}</strong></p>" +
                     $"<p>This code expires in 15 minutes. " +
                     $"Do not share this code with anyone.</p>",
            ct: cancellationToken);

        // ── 5. Return identical response ──────────────────────────────────────
        //
        // ALWAYS return the same message — prevents email enumeration.
        return Result<bool>.Success(
            true,
            "If an account with that email exists, a reset code has been sent.");
    }
}