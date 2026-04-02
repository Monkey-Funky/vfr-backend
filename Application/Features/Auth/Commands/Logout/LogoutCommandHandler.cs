namespace Application.Features.Auth.Commands.Logout;

/// <summary>
/// Handles retailer logout by revoking the stored refresh token.
///
/// FLOW:
///   1. Extract RetailerId from the JWT via ICurrentUserService
///   2. Load the RetailerAccount
///   3. Call RevokeAllRefreshTokens() — nulls RefreshTokenHash and RefreshTokenExpiresAt
///   4. Persist
///
/// The access token is NOT invalidated here because JWT access tokens are stateless.
/// They remain technically valid until they expire (15 minutes maximum).
/// The short TTL is the architectural mitigation for this limitation.
/// </summary>
public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ITokenService tokenService,
        ILogger<LogoutCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        LogoutCommand command,
        CancellationToken cancellationToken)
    {
        Guid? retailerId = _currentUserService.RetailerId;

        if (!retailerId.HasValue)
        {
            // Level 2: Try to extract from the raw Authorization header token
            // (handles expired access tokens — common 15-min expiry scenario)
            var rawToken = _currentUserService.GetRawBearerToken();
            if (rawToken is not null)
            {
                ClaimsPrincipal? principal = _tokenService.GetClaimsFromExpiredToken(rawToken);

                string? subClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? principal?.FindFirst("sub")?.Value;

                if (Guid.TryParse(subClaim, out Guid idFromExpiredToken) && idFromExpiredToken != Guid.Empty)
                {
                    retailerId = idFromExpiredToken;

                    _logger.LogInformation(
                        "Logout — RetailerId resolved from expired token (fallback path). " +
                        "RetailerId: {RetailerId}", retailerId);
                }
            }
        }

        if (!retailerId.HasValue)
        {
            // Level 3: No valid identity at all — idempotent success
            // The session is already effectively terminated on the client side.
            // No DB row to update — do not throw, do not error.
            _logger.LogInformation(
                "Logout called with no valid identity (no token or unverifiable token). " +
                "Returning idempotent success — treating as already logged out.");

            return Result<bool>.Success(true, "Logged out successfully.");
        }

        // ── 2. Load the RetailerAccount ────────────────────────────────────────
        //
        // Use IgnoreQueryFilters — we need to handle soft-deleted accounts gracefully
        // rather than throwing NotFoundException (which maps to 404, not a clean logout).
        RetailerAccount? account = await _unitOfWork
            .Repository<RetailerAccount>()
            .GetByIdAsync(retailerId.Value, cancellationToken);

        if (account is null || account.IsDeleted)
        {
            // Account doesn't exist or is already deleted — no session to revoke.
            // Return idempotent success.
            _logger.LogWarning(
                "Logout — account not found or soft-deleted. RetailerId: {RetailerId}. " +
                "Returning idempotent success.", retailerId);

            return Result<bool>.Success(true, "Logged out successfully.");
        }

        // ── 3. Revoke refresh token ────────────────────────────────────────────
        //
        // Nulls RefreshTokenHash and RefreshTokenExpiresAt.
        // Any subsequent RefreshToken request with the old token will fail the
        // "no active refresh token" check in RefreshTokenCommandHandler.
        account.RevokeAllRefreshTokens();

        // ── 4. Persist ────────────────────────────────────────────────────────
        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Retailer logged out. RetailerId: {RetailerId}", retailerId);

        return Result<bool>.Success(true, "Logged out successfully.");
    }
}