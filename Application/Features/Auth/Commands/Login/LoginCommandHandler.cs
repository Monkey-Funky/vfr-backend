using Application.Features.Auth.DTOs;
using Application.Features.Auth.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Auth.Commands.Login;

/// <summary>
/// Handles email + password authentication for a retailer.
///
/// FLOW:
///   1. Find retailer by email (case-insensitive), excluding soft-deleted accounts
///   2. Guard: account not found → generic "Invalid email or password" (prevents email enumeration)
///   3. Guard: lockout checked BEFORE status (FIX F-04 from P-013) — prevents status oracle
///   4. Guard: account status (PendingEmailVerification, Suspended, PendingDeletion, Deleted)
///   5. Verify BCrypt password hash
///   6. On failure: increment AccessFailedCount → explicit UpdateAsync → SaveChangesAsync → throw
///   7. On success: reset failed count → issue JWT + refresh token → explicit UpdateAsync → save
///   8. Return AuthTokenResponse
/// </summary>
public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, Result<AuthTokenResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<LoginCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthTokenResponse>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        // ── 1. Find retailer by email (case-insensitive) ──────────────────────
        //
        // Query by normalised lower-case email so "User@Domain.com" and "user@domain.com"
        // resolve to the same account.
        // We do NOT filter by status here so that the lockout check (step 3) can fire
        // first for any account, regardless of its status.
        //
        // Email is a unique column (guaranteed 0 or 1 rows). FirstOrDefaultAsync is used
        // here because it matches the IRepository contract used by all tests and callers.
        RetailerAccount? retailer = await _unitOfWork
            .Repository<RetailerAccount>()
            .FirstOrDefaultAsync(
                r => r.Email.ToLower() == command.Email.ToLower().Trim()
                  && !r.IsDeleted,
                cancellationToken);

        // ── 2. Guard: account not found ───────────────────────────────────────
        //
        // Return a generic message. NEVER say "email not found" — that would
        // allow an attacker to enumerate which emails are registered.
        //
        // Previously used UnauthorizedException which maps to HTTP 403 Forbidden.
        if (retailer is null)
        {
            _logger.LogWarning(
                "Login failed — email not found. EmailDomain: {EmailDomain}",
                GetEmailDomain(command.Email));

            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // ── 3. Guard: lockout (checked BEFORE status — FIX F-04) ─────────────
        //
        // Checking lockout before status prevents an attacker from determining the
        // account status (e.g. PendingEmailVerification vs Suspended) by comparing
        // which error they receive. Lockout always fires first, masking the status.
        //
        if (retailer.IsLockedOut())
        {
            var remaining = (int)Math.Ceiling(
                (retailer.LockoutEndAt!.Value - DateTime.UtcNow).TotalMinutes);

            _logger.LogWarning(
                "Login failed — account locked. RetailerId: {RetailerId}. " +
                "LockoutEnd: {LockoutEnd}. RemainingMinutes: {Remaining}",
                retailer.Id, retailer.LockoutEndAt, remaining);

            throw new UnauthorizedAccessException(
                $"Your account is temporarily locked due to too many failed login attempts. " +
                $"Try again in {remaining} minute(s).");
        }

        // ── 4. Guard: account status ──────────────────────────────────────────
        //
        //   - PendingEmailVerification → EMAIL_NOT_VERIFIED (client can guide user to resend)
        //   - Suspended                → ACCOUNT_SUSPENDED  (client can direct user to support)
        //   - PendingDeletion/Deleted  → ACCOUNT_INACTIVE   (generic catch-all)
        //
        // All three map to HTTP 422 via BusinessRuleException → ExceptionHandlingMiddleware.

        if (retailer.AccountStatus == RetailerAccount.Status.PendingEmailVerification)
        {
            _logger.LogWarning(
                "Login failed — email not verified. RetailerId: {RetailerId}", retailer.Id);

            throw new BusinessRuleException(
                "EMAIL_NOT_VERIFIED",
                "Your email address has not been verified. " +
                "Please complete Step 2 of registration to activate your account.");
        }

        if (retailer.AccountStatus == RetailerAccount.Status.Suspended)
        {
            _logger.LogWarning(
                "Login failed — account suspended. RetailerId: {RetailerId}", retailer.Id);

            throw new BusinessRuleException(
                "ACCOUNT_SUSPENDED",
                "Your account has been suspended. " +
                "Please contact support for assistance.");
        }

        if (retailer.AccountStatus is
                RetailerAccount.Status.PendingDeletion or
                RetailerAccount.Status.Deleted)
        {
            _logger.LogWarning(
                "Login failed — account status is {Status}. RetailerId: {RetailerId}",
                retailer.AccountStatus, retailer.Id);

            throw new BusinessRuleException(
                "ACCOUNT_INACTIVE",
                $"Your account is currently {retailer.AccountStatus.ToLower()}. " +
                "Please contact support if you believe this is an error.");
        }

        // ── 5. Verify BCrypt password hash ────────────────────────────────────
        //
        // BCrypt.Verify is inherently constant-time — it always runs the full hash
        // computation regardless of where the mismatch occurs. This resists timing attacks.
        bool passwordValid = BCrypt.Net.BCrypt.Verify(command.Password, retailer.PasswordHash);

        if (!passwordValid)
        {
            // ── 6. On failure: increment counter and persist ──────────────────
            //
            // IncrementFailedLoginCount also sets LockoutEndAt when count reaches 10.
            retailer.IncrementFailedLoginCount();

            await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(retailer, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Login failed — wrong password. RetailerId: {RetailerId}. " +
                "FailedAttempts: {FailedCount}. Locked: {IsLocked}",
                retailer.Id, retailer.AccessFailedCount, retailer.IsLockedOut());

            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // ── 7. On success: reset lockout state, issue tokens, persist ─────────

        // Reset any previous failed-login counter and lockout state.
        retailer.ResetFailedLoginCount();

        // Generate a new RS256 access token.
        string accessToken = _tokenService.GenerateAccessToken(retailer);

        // Generate a new raw (unhashed) refresh token — 64 bytes of cryptographic randomness.
        string rawRefreshToken = _tokenService.GenerateRefreshToken();

        // RememberMe = true → 30-day refresh TTL; false → 7-day TTL.
        int refreshExpiryDays = command.RememberMe ? 30 : 7;
        DateTime refreshExpiresAt = DateTime.UtcNow.AddDays(refreshExpiryDays);

        // Hash the refresh token before storing — raw token goes only to the client.
        string hashedRefreshToken = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken, workFactor: 12);

        // Store the hashed refresh token. Also persist IsRememberMeSession so that
        // subsequent rotations in RefreshTokenCommandHandler preserve the correct TTL.
        retailer.UpdateRefreshToken(hashedRefreshToken, refreshExpiresAt, command.RememberMe);

        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(retailer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Login successful. RetailerId: {RetailerId}. Email: {EmailDomain}. " +
            "RememberMe: {RememberMe}. RefreshExpiry: {RefreshExpiry} days",
            retailer.Id, GetEmailDomain(retailer.Email), command.RememberMe, refreshExpiryDays);

        // ── 8. Return ─────────────────────────────────────────────────────────
        AuthTokenResponse response = retailer.ToAuthResponse(accessToken, rawRefreshToken);
        return Result<AuthTokenResponse>.Success(response, "Login successful.");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns only the domain portion of an email for safe logging.
    /// e.g. "owner@acme.com" → "@acme.com"
    /// Never log the full email address per 08-LoggingStrategy.md §4.
    /// </summary>
    private static string GetEmailDomain(string email)
    {
        var idx = email.IndexOf('@');
        return idx >= 0 ? email[idx..] : "[unknown-domain]";
    }
}