// src/Application/Features/Auth/Commands/Login/LoginCommandHandler.cs

using Application.Features.Auth.DTOs;
using Application.Features.Auth.Mappings;

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
///
/// FIX F-04 (from P-013) — Lockout order:
///   Lockout check (step 3) runs before all status checks (step 4) so that
///   an attacker cannot infer account status from which error code they receive.
///   A locked account always returns ACCOUNT_LOCKED regardless of its status.
///
/// FIX F-09 (from P-013) — Explicit UpdateAsync on both paths:
///   Previously SaveChangesAsync was called without UpdateAsync. If the repository
///   ever uses AsNoTracking, the in-memory mutation is never sent to the DB.
///   Now UpdateAsync is called explicitly on both the failed and success paths
///   for correctness and consistency with all other command handlers.
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
        // We do NOT filter by status here so that lockout check (step 3) can fire
        // first for any account, regardless of its status.
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
        if (retailer is null)
        {
            _logger.LogWarning(
                "Login failed — email not found. Email: {Email}", command.Email);

            throw new UnauthorizedException("Invalid email or password.");
        }

        // ── 3. Guard: lockout — MUST run BEFORE all status checks ─────────────
        //
        // Checking lockout first prevents a status-oracle attack: if status were
        // checked first, an attacker could compare error codes (EMAIL_NOT_VERIFIED vs
        // ACCOUNT_LOCKED) to determine the lifecycle state of a targeted email address.
        // With lockout first, a locked account always returns ACCOUNT_LOCKED regardless.
        if (retailer.IsLockedOut())
        {
            var remainingMinutes = (int)Math.Ceiling(
                (retailer.LockoutEndAt!.Value - DateTime.UtcNow).TotalMinutes);

            _logger.LogWarning(
                "Login failed — account locked. RetailerId: {RetailerId}. " +
                "Lockout ends: {LockoutEndAt}", retailer.Id, retailer.LockoutEndAt);

            throw new BusinessRuleException(
                "ACCOUNT_LOCKED",
                $"Your account is temporarily locked due to too many failed login attempts. " +
                $"Please try again in {remainingMinutes} minute(s).");
        }

        // ── 4. Guard: account status ──────────────────────────────────────────
        //
        // PendingEmailVerification = retailer started Step 1 but never completed Step 2.
        // Login is blocked until the account transitions to Active via RegisterStep2.
        if (retailer.AccountStatus == RetailerAccount.Status.PendingEmailVerification)
        {
            _logger.LogWarning(
                "Login failed — registration not complete. RetailerId: {RetailerId}",
                retailer.Id);

            throw new BusinessRuleException(
                "EMAIL_NOT_VERIFIED",
                "Your registration is not complete. Please finish Step 2 of the registration " +
                "process. Check your inbox for the step token or start over.");
        }

        if (retailer.AccountStatus is
                RetailerAccount.Status.Suspended or
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

            // FIX F-09: Explicit UpdateAsync ensures the mutation is tracked and
            // persisted even if the Repository uses AsNoTracking queries.
            await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(retailer, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Login failed — wrong password. RetailerId: {RetailerId}. " +
                "Failed attempts: {FailedCount}. Locked: {IsLocked}",
                retailer.Id, retailer.AccessFailedCount, retailer.IsLockedOut());

            // Same generic error message as step 2 (account-not-found)
            // to prevent password-vs-email-not-found enumeration.
            throw new UnauthorizedException("Invalid email or password.");
        }

        // ── 7. On success: reset lockout state, issue tokens, persist ─────────

        // Reset any previous failed-login counter and lockout state.
        retailer.ResetFailedLoginCount();

        // Generate a new RS256 access token
        string accessToken = _tokenService.GenerateAccessToken(retailer);

        // Generate a new raw (unhashed) refresh token — 64 bytes of cryptographic randomness
        string rawRefreshToken = _tokenService.GenerateRefreshToken();

        // RememberMe = true → 30-day refresh TTL; false → 7-day TTL
        int refreshExpiryDays = command.RememberMe ? 30 : 7;
        DateTime refreshExpiresAt = DateTime.UtcNow.AddDays(refreshExpiryDays);

        // Hash the refresh token before storing — raw token goes only to the client
        string hashedRefreshToken = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken, workFactor: 12);

        // Store the hashed refresh token. Also persist IsRememberMeSession so that
        // subsequent rotations in RefreshTokenCommandHandler preserve the correct TTL.
        retailer.UpdateRefreshToken(hashedRefreshToken, refreshExpiresAt, command.RememberMe);

        // FIX F-09: Explicit UpdateAsync on the success path too (consistent with all
        // other handlers and correct regardless of change-tracking behaviour).
        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(retailer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Login successful. RetailerId: {RetailerId}. Email: {Email}. " +
            "RememberMe: {RememberMe}. RefreshExpiry: {RefreshExpiry} days",
            retailer.Id, retailer.Email, command.RememberMe, refreshExpiryDays);

        // ── 8. Return ─────────────────────────────────────────────────────────
        AuthTokenResponse response = retailer.ToAuthResponse(accessToken, rawRefreshToken);
        return Result<AuthTokenResponse>.Success(response, "Login successful.");
    }
}