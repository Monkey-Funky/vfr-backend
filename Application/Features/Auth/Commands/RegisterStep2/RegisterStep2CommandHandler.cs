// src/Application/Features/Auth/Commands/RegisterStep2/RegisterStep2CommandHandler.cs

using Application.Features.Auth.DTOs;
using Application.Features.Auth.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Auth.Commands.RegisterStep2;

/// <summary>
/// Handles Step 2 of two-step retailer registration.
///
/// FLOW:
///   1. Validate and decode TempStepToken — verify signature, expiry, token_type claim
///   2. Extract and validate temp_account_id and step claims
///   3. Load the partial RetailerAccount from DB
///   4. Verify it is still in PendingEmailVerification status (replay attack guard)
///   5. Upload brand logo to S3 (if provided)
///   6. Call account.CompleteRegistration(...) — sets Status = Active, IsEmailVerified = true
///   7. Atomic transaction via ExecuteInTransactionAsync:
///        a. Update RetailerAccount (Status → Active)
///        b. Seed NotificationPreference record
///        c. SaveChangesAsync inside the lambda
///        d. Commit (by ExecuteInTransactionAsync) — or rollback on failure
///   8. On transaction failure: delete the uploaded S3 object (FIX F-06 — orphan prevention)
///   9. Send verification welcome email (fire-and-forget — does not block response)
///  10. Generate tokens, store hashed refresh token, return AuthTokenResponse
///
/// FIX F-02 — token_type claim validation:
///   Step tokens carry a "token_type":"step" claim validated here so no other
///   token type is accepted even if it passes HS256 signature verification.
///
/// FIX F-05 — IsEmailVerified:
///   CompleteRegistration() now calls MarkEmailVerified() internally so
///   IsEmailVerified = true for all email-registered accounts after Step 2.
///
/// FIX F-06 — Orphaned S3 objects on transaction failure:
///   If the DB transaction rolls back after a logo was already uploaded to S3,
///   we delete the S3 object before rethrowing, so no orphaned objects accumulate.
///
/// TRANSACTION COMPATIBILITY:
///   Uses ExecuteInTransactionAsync (instead of BeginTransactionAsync / CommitTransactionAsync)
///   to be compatible with NpgsqlRetryingExecutionStrategy (EnableRetryOnFailure).
///   See UnitOfWork.ExecuteInTransactionAsync for the full explanation.
/// </summary>
public sealed class RegisterStep2CommandHandler
    : IRequestHandler<RegisterStep2Command, Result<AuthTokenResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEmailService _emailService;
    private readonly ILogger<RegisterStep2CommandHandler> _logger;

    public RegisterStep2CommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        IFileStorageService fileStorageService,
        IEmailService emailService,
        ILogger<RegisterStep2CommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _fileStorageService = fileStorageService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<AuthTokenResponse>> Handle(
        RegisterStep2Command command,
        CancellationToken cancellationToken)
    {
        // ── 1. Validate and decode the step token ─────────────────────────────
        //
        // ValidateTempStepToken verifies the HS256 signature, expiry, issuer, and audience.
        // Returns null if the token is invalid in any way.
        ClaimsPrincipal? principal = _tokenService.ValidateTempStepToken(command.TempStepToken);

        if (principal is null)
        {
            throw new UnauthorizedException(
                "The registration step token is invalid or has expired. " +
                "Please restart registration from Step 1.");
        }

        // FIX F-02: Validate the explicit token_type claim.
        // A valid HS256 signature alone is not enough — the token must declare itself
        // as a "step" token, so other token types cannot be substituted here.
        string? tokenType = principal.FindFirst("token_type")?.Value;
        if (tokenType != "step")
        {
            _logger.LogWarning(
                "RegisterStep2 — token_type claim is '{TokenType}', expected 'step'.", tokenType);
            throw new UnauthorizedException(
                "The provided token is not a valid registration step token.");
        }

        // ── 2. Extract and validate temp_account_id and step claims ──────────
        string? tempIdClaim = principal.FindFirst("temp_account_id")?.Value;
        if (!Guid.TryParse(tempIdClaim, out Guid tempAccountId) || tempAccountId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "The registration step token contains invalid account data.");
        }

        string? stepClaim = principal.FindFirst("step")?.Value;
        if (stepClaim != "1")
        {
            throw new UnauthorizedException(
                "The registration step token is not a valid Step 1 token.");
        }

        // ── 3. Load the partial account ───────────────────────────────────────
        RetailerAccount? account = await _unitOfWork
            .Repository<RetailerAccount>()
            .GetByIdAsync(tempAccountId, cancellationToken);

        if (account is null || account.IsDeleted)
        {
            throw new NotFoundException("RetailerAccount", tempAccountId);
        }

        // ── 4. Verify account is still awaiting completion ────────────────────
        //
        // Replay attack guard: if Step 2 was already completed (status is Active),
        // reject the request. This also prevents cross-account token reuse because
        // the tempAccountId in the token is bound to a specific DB row.
        if (account.AccountStatus != RetailerAccount.Status.PendingEmailVerification)
        {
            throw new BusinessRuleException(
                "REGISTRATION_ALREADY_COMPLETED",
                "This registration has already been completed. Please log in instead.");
        }

        // ── 5. Upload brand logo (optional) ───────────────────────────────────
        //
        // Uploaded BEFORE the transaction intentionally:
        //   - S3 uploads can be slow (network I/O) and should not hold a DB transaction open.
        //   - If the upload fails, no DB changes have been made — safe to propagate exception.
        //   - If the upload succeeds but the DB transaction fails (rare), we clean up in catch.
        string? brandLogoUrl = null;

        if (command.BrandLogoStream is not null && command.BrandLogoFileName is not null)
        {
            // Storage filename: {accountId}_{originalFileName} for S3 key uniqueness.
            string storageFileName = $"{account.Id:N}_{command.BrandLogoFileName}";

            brandLogoUrl = await _fileStorageService.UploadAsync(
                command.BrandLogoStream,
                storageFileName,
                folder: "brand-logos",
                ct: cancellationToken);

            _logger.LogInformation(
                "Brand logo uploaded. RetailerId: {RetailerId}. Url: {Url}",
                account.Id, brandLogoUrl);
        }

        // ── 6. Complete registration (domain method) ───────────────────────────
        //
        // Sets AccountStatus = Active, BusinessType, Has3DModels, BrandLogoUrl.
        // FIX F-05: Also sets IsEmailVerified = true (see CompleteRegistration implementation).
        account.CompleteRegistration(command.BusinessType, command.Has3DModels, brandLogoUrl);

        // ── 7. Atomic transaction: update account + seed NotificationPreference ─
        //
        // Using ExecuteInTransactionAsync (NOT BeginTransactionAsync) because
        // NpgsqlRetryingExecutionStrategy (EnableRetryOnFailure) does not allow
        // user-initiated transactions outside of CreateExecutionStrategy().ExecuteAsync().
        // See UnitOfWork.ExecuteInTransactionAsync for the full explanation.
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // Update the retailer account (Status → Active, business details)
                await _unitOfWork.Repository<RetailerAccount>()
                    .UpdateAsync(account, ct);

                // Seed default notification preferences atomically.
                // Every Active account must have a preference row from its first moment.
                NotificationPreference defaultPrefs =
                    NotificationPreference.CreateDefault(account.Id);

                await _unitOfWork.Repository<NotificationPreference>()
                    .AddAsync(defaultPrefs, ct);

                // SaveChangesAsync MUST be called inside the lambda (before commit).
                await _unitOfWork.SaveChangesAsync(ct);

            }, cancellationToken);
        }
        catch
        {
            // FIX F-06: If the DB transaction failed AFTER the S3 upload succeeded,
            // delete the orphaned S3 object so it does not accumulate in the bucket.
            // Use CancellationToken.None — the request token may already be cancelled.
            if (brandLogoUrl is not null)
            {
                try
                {
                    await _fileStorageService.DeleteAsync(brandLogoUrl, CancellationToken.None);
                    _logger.LogWarning(
                        "S3 logo deleted after transaction failure. " +
                        "RetailerId: {RetailerId}. Url: {Url}",
                        account.Id, brandLogoUrl);
                }
                catch (Exception s3Ex)
                {
                    // Log but do not rethrow — the original DB exception is more important.
                    _logger.LogError(s3Ex,
                        "Failed to delete orphaned S3 object after transaction failure. " +
                        "RetailerId: {RetailerId}. Url: {Url}",
                        account.Id, brandLogoUrl);
                }
            }

            throw; // Rethrow the original DB exception
        }

        _logger.LogInformation(
            "Registration Step 2 complete. RetailerId: {RetailerId}. Email: {Email}",
            account.Id, account.Email);

        // ── 8. Send welcome email (fire-and-forget) ───────────────────────────
        //
        // Deliberately NOT awaited. Email failure must NOT roll back the account
        // activation — the account is already Active in the DB at this point.
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendEmailAsync(
                    to: account.Email,
                    subject: "Welcome to VFR — Your account is active!",
                    body: $"<p>Hi {account.FullName},</p>" +
                             $"<p>Your Virtual Fitting Room retailer account for " +
                             $"<strong>{account.BrandName}</strong> is now active!</p>" +
                             $"<p>Thank you for joining us. You can now log in and start " +
                             $"managing your store.</p>",
                    ct: CancellationToken.None);  // Independent of request lifetime
            }
            catch (Exception ex)
            {
                // Log but swallow — welcome email is non-critical
                _logger.LogError(ex,
                    "Failed to send welcome email. RetailerId: {RetailerId}. Email: {Email}",
                    account.Id, account.Email);
            }
        }, CancellationToken.None);

        // ── 9. Issue tokens ───────────────────────────────────────────────────
        string accessToken = _tokenService.GenerateAccessToken(account);
        string rawRefreshToken = _tokenService.GenerateRefreshToken();

        // Registration sessions always start as non-RememberMe (7-day TTL).
        // The retailer can choose RememberMe on their first explicit login.
        DateTime refreshExpiresAt = DateTime.UtcNow.AddDays(7);
        string hashedRefreshToken = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken, workFactor: 12);

        account.UpdateRefreshToken(hashedRefreshToken, refreshExpiresAt, rememberMe: false);

        // Persist the refresh token — this is a simple single-row update outside the
        // transaction (the account activation is already committed above).
        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── 10. Return ────────────────────────────────────────────────────────
        AuthTokenResponse response = account.ToAuthResponse(accessToken, rawRefreshToken);
        return Result<AuthTokenResponse>.Success(response, "Registration complete. Welcome!");
    }
}