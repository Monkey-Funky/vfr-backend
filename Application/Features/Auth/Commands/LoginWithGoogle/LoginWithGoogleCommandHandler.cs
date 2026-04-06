// src/Application/Features/Auth/Commands/LoginWithGoogle/LoginWithGoogleCommandHandler.cs

using Application.Features.Auth.DTOs;
using Application.Features.Auth.Mappings;
using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Commands.LoginWithGoogle;

/// <summary>
/// Handles Google OAuth sign-in for retailers.
///
/// FLOW:
///   1. Validate the Google ID token — extract email, GoogleId, FullName from verified claims
///   2. Try to find an existing retailer by GoogleId OR email (excludes soft-deleted)
///   3a. If found: validate status → link GoogleId if not yet set → issue tokens → persist
///   3b. If not found: create new Active account → seed NotificationPreference → issue tokens
///   4. Return AuthTokenResponse
///
/// FIX F-03 (from P-013) — PendingEmailVerification status blocked:
///   A retailer who started Step-1 email registration and then tries Google OAuth with the
///   same email is now blocked (status = PendingEmailVerification), preventing them from
///   bypassing the Step-2 requirement and receiving tokens for an incomplete account.
///
/// FIX F-04 (from P-013) — NotificationPreference seeded for new Google accounts:
///   New accounts created via Google OAuth are now wrapped in a transaction that atomically
///   creates both the RetailerAccount and the NotificationPreference row. Without this,
///   any feature reading NotificationPreference for a Google account would find no row.
///
/// TRANSACTION COMPATIBILITY:
///   Uses ExecuteInTransactionAsync (NOT BeginTransactionAsync) to be compatible with
///   NpgsqlRetryingExecutionStrategy (EnableRetryOnFailure). See UnitOfWork for details.
/// </summary>
public sealed class LoginWithGoogleCommandHandler
    : IRequestHandler<LoginWithGoogleCommand, Result<AuthTokenResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginWithGoogleCommandHandler> _logger;

    public LoginWithGoogleCommandHandler(
        IUnitOfWork unitOfWork,
        IGoogleAuthService googleAuthService,
        ITokenService tokenService,
        ILogger<LoginWithGoogleCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _googleAuthService = googleAuthService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthTokenResponse>> Handle(
        LoginWithGoogleCommand command,
        CancellationToken cancellationToken)
    {
        // ── 1. Validate Google ID token ───────────────────────────────────────
        //
        // IGoogleAuthService.ValidateAsync throws ExternalServiceException if the token
        // is invalid, expired, or Google's JWKS endpoint is unreachable.
        // All fields come from verified Google claims — never from client-provided data.
        GoogleUserInfo googleUser =
            await _googleAuthService.ValidateAsync(command.GoogleIdToken, cancellationToken);

        _logger.LogInformation(
            "Google token validated. Email: {Email}. GoogleId: {GoogleId}",
            googleUser.Email, googleUser.GoogleId);

        // ── 2. Find existing retailer ─────────────────────────────────────────
        //
        // Try GoogleId first (faster — indexed column), then fall back to email
        // (handles the case where the retailer registered via email first and is
        // now linking Google for the first time).
        //
        // NOTE: The global query filter (HasQueryFilter(r => !r.IsDeleted)) is active.
        // The explicit !r.IsDeleted in the predicate is redundant but kept for clarity.
        RetailerAccount? retailer = await _unitOfWork
            .Repository<RetailerAccount>()
            .FirstOrDefaultAsync(
                r => (r.GoogleId == googleUser.GoogleId
                   || r.Email.ToLower() == googleUser.Email.ToLower())
                  && !r.IsDeleted,
                cancellationToken);

        // Track whether we are creating a brand-new account
        bool isNewAccount = false;

        if (retailer is not null)
        {
            // ── 3a. Existing account found ────────────────────────────────────

            // Block access for non-Active account statuses.
            // A retailer who started email registration (Step 1) must complete Step 2
            // before they can use Google OAuth with the same email.
            if (retailer.AccountStatus is
                    RetailerAccount.Status.PendingEmailVerification or
                    RetailerAccount.Status.Suspended or
                    RetailerAccount.Status.PendingDeletion or
                    RetailerAccount.Status.Deleted)
            {
                string message = retailer.AccountStatus == RetailerAccount.Status.PendingEmailVerification
                    ? "Your account registration is incomplete. Please complete Step 2 of the " +
                      "registration process before signing in with Google."
                    : $"Your account is currently {retailer.AccountStatus.ToLower()}. " +
                      "Please contact support for assistance.";

                _logger.LogWarning(
                    "Google login blocked — account status is {Status}. RetailerId: {RetailerId}",
                    retailer.AccountStatus, retailer.Id);

                throw new BusinessRuleException("ACCOUNT_INACTIVE", message);
            }

            // Link GoogleId if this retailer registered via email and is signing in
            // with Google for the first time (first-time OAuth link).
            if (retailer.GoogleId is null)
            {
                retailer.SetGoogleId(googleUser.GoogleId);
                _logger.LogInformation(
                    "Linked Google account to existing email-registered retailer. " +
                    "RetailerId: {RetailerId}", retailer.Id);
            }
        }
        else
        {
            // ── 3b. New account — create via Google OAuth ─────────────────────
            //
            // Generate a unique initial BrandName from the email prefix + short GUID.
            // The retailer can update their brand name later via profile management.
            string emailPrefix = googleUser.Email.Split('@')[0];
            string brandName = $"{emailPrefix}-{Guid.NewGuid().ToString("N")[..8]}";

            retailer = RetailerAccount.CreateWithGoogle(
                googleUser.FullName,
                googleUser.Email,
                googleUser.GoogleId,
                brandName);

            isNewAccount = true;
        }

        // ── 4. Issue tokens ────────────────────────────────────────────────────
        //
        // Tokens are generated before the DB write so that the access token is ready
        // immediately after the transaction commits (no extra round trip needed).
        // For new accounts, retailer.Id is pre-set by BaseEntity (Guid.NewGuid()) before
        // AddAsync is called — the ID is stable and correct in the token.
        string accessToken = _tokenService.GenerateAccessToken(retailer);
        string rawRefreshToken = _tokenService.GenerateRefreshToken();

        // Standard 7-day TTL for Google OAuth sessions (no RememberMe option here).
        DateTime refreshExpiresAt = DateTime.UtcNow.AddDays(7);
        string hashedRefreshToken = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken, workFactor: 12);

        retailer.UpdateRefreshToken(hashedRefreshToken, refreshExpiresAt, rememberMe: false);

        // ── 5. Persist ────────────────────────────────────────────────────────

        if (isNewAccount)
        {
            // FIX F-03: New Google accounts must have a NotificationPreference row.
            // Wrap both the account insert and the preference insert in one transaction
            // so they are committed atomically.
            //
            // FIX F-03: DbUpdateException safety net — brand name or email collision.
            // Although the brand name is auto-generated with GUID entropy (very rare collision),
            // we still catch PostgreSQL unique constraint violations and return a user-friendly
            // error instead of propagating an unhandled 500.
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await _unitOfWork.Repository<RetailerAccount>()
                        .AddAsync(retailer, ct);

                    NotificationPreference defaultPrefs =
                        NotificationPreference.CreateDefault(retailer.Id);

                    await _unitOfWork.Repository<NotificationPreference>()
                        .AddAsync(defaultPrefs, ct);

                    // SaveChangesAsync MUST be called inside the lambda, before commit.
                    await _unitOfWork.SaveChangesAsync(ct);

                }, cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _logger.LogWarning(ex,
                    "LoginWithGoogle — unique constraint violation when creating new account. " +
                    "Email: {Email}. This may indicate a concurrent registration or brand name collision.",
                    googleUser.Email);

                throw new BusinessRuleException(
                    "ACCOUNT_CONFLICT",
                    "An account with this email or brand name already exists. " +
                    "If you already have an account, please sign in using your email and password.");
            }

            _logger.LogInformation(
                "New retailer created via Google OAuth with NotificationPreference seeded. " +
                "Email: {Email}. RetailerId: {RetailerId}",
                googleUser.Email, retailer.Id);
        }
        else
        {
            // Existing account — update the refresh token (and optionally the linked GoogleId).
            // No transaction needed for a single-row update.
            try
            {
                await _unitOfWork.Repository<RetailerAccount>()
                    .UpdateAsync(retailer, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _logger.LogWarning(ex,
                    "LoginWithGoogle — unexpected unique constraint violation on existing account update. " +
                    "RetailerId: {RetailerId}", retailer.Id);

                // This should be extremely rare for an existing account update,
                // but we guard it anyway to prevent unhandled 500.
                throw new BusinessRuleException(
                    "ACCOUNT_CONFLICT",
                    "A conflict occurred while updating your account. Please try again.");
            }
        }

        _logger.LogInformation(
            "Google login successful. RetailerId: {RetailerId}. NewAccount: {IsNew}",
            retailer.Id, isNewAccount);

        // ── 6. Return ─────────────────────────────────────────────────────────
        AuthTokenResponse response = retailer.ToAuthResponse(accessToken, rawRefreshToken);
        return Result<AuthTokenResponse>.Success(response, "Google login successful.");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the <see cref="DbUpdateException"/> was caused by a
    /// PostgreSQL unique constraint violation (SQLSTATE 23505).
    /// Uses type name matching to avoid a hard Npgsql dependency in the Application project.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var innerEx = ex.InnerException;
        if (innerEx is null) return false;

        if (innerEx.GetType().Name == "PostgresException")
        {
            var sqlStateProperty = innerEx.GetType().GetProperty("SqlState");
            var sqlState = sqlStateProperty?.GetValue(innerEx) as string;
            return sqlState == "23505";
        }

        return innerEx.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || innerEx.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}