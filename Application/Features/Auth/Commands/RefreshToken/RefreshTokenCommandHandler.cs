// src/Application/Features/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs
using Application.Features.Auth.DTOs;
using Application.Features.Auth.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Handles token rotation — exchanges an expired access token + valid refresh token
/// for a new access + refresh token pair.
///
/// FLOW:
///   1. Extract RetailerId from the (possibly expired) access token without lifetime check
///   2. Load the RetailerAccount
///   3. Verify the account has an active refresh token
///   4. Verify the raw refresh token against the stored BCrypt hash
///   5. Verify the refresh token has not expired (DB expiry check)
///   6. Issue new access + refresh tokens (rotation — old token invalidated)
///   7. Persist the new hashed refresh token (with optimistic concurrency guard)
///   8. Return AuthTokenResponse
///
/// FIX F-03 — Concurrent refresh token race condition (TOCTOU):
///   Two simultaneous refresh requests with the same token previously both passed
///   BCrypt.Verify (hash unchanged) and both wrote new tokens, causing one client
///   to receive a token that was immediately invalidated by the second write.
///
///   Fix: UseXminAsConcurrencyToken() on RetailerAccountConfiguration causes EF Core
///   to include the PostgreSQL xmin value in the WHERE clause of every UPDATE:
///     UPDATE retailer_accounts SET ... WHERE id = @id AND xmin = @read_xmin
///   If a concurrent request committed first, the xmin changed and EF Core finds
///   0 rows affected, throwing DbUpdateConcurrencyException. This is caught here
///   and returned to the client as 401 "concurrent refresh detected, please retry."
///
/// FIX F-07 — RememberMe TTL preservation:
///   Previously, every rotation hardcoded a 7-day TTL, silently degrading a
///   30-day RememberMe session after the first rotation. Now, IsRememberMeSession
///   is read from the entity (persisted during login) and the original TTL is kept.
/// </summary>
public sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, Result<AuthTokenResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthTokenResponse>> Handle(
    RefreshTokenCommand command,
    CancellationToken cancellationToken)
    {
        // ── 1. Extract RetailerId from expired access token ────────────────────────────
        ClaimsPrincipal? principal = _tokenService.GetClaimsFromExpiredToken(command.AccessToken);

        if (principal is null)
        {
            _logger.LogWarning("RefreshToken failed — access token is structurally invalid.");
            throw new UnauthorizedException("The access token is invalid.");
        }

        string? subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? principal.FindFirst("sub")?.Value;

        if (!Guid.TryParse(subClaim, out Guid retailerId) || retailerId == Guid.Empty)
        {
            _logger.LogWarning("RefreshToken failed — 'sub' claim is missing or invalid.");
            throw new UnauthorizedException("The access token does not contain a valid identity.");
        }

        // ── 2. Load the RetailerAccount ────────────────────────────────────────────────
        RetailerAccount? account = await _unitOfWork
            .Repository<RetailerAccount>()
            .GetByIdAsync(retailerId, cancellationToken);

        if (account is null || account.IsDeleted)
        {
            _logger.LogWarning(
                "RefreshToken failed — account not found. RetailerId: {RetailerId}", retailerId);
            throw new UnauthorizedException("Account not found.");
        }

        // ── FIX F-02: Guard — account must be Active ───────────────────────────────────
        //
        // A suspended or PendingDeletion account must NOT receive new tokens.
        // The login endpoint blocks these statuses; the refresh endpoint must too,
        // otherwise an admin suspension is bypassed the moment the retailer has
        // a valid refresh token in hand.
        if (account.AccountStatus != RetailerAccount.Status.Active)
        {
            _logger.LogWarning(
                "RefreshToken failed — account status is {Status}. RetailerId: {RetailerId}",
                account.AccountStatus, retailerId);
            throw new UnauthorizedException(
                "Your account is no longer active. Please log in again for assistance.");
        }

        // ── 3. Guard: no active refresh token ─────────────────────────────────────────
        if (account.RefreshTokenHash is null || account.RefreshTokenExpiresAt is null)
        {
            _logger.LogWarning(
                "RefreshToken failed — no active refresh token. RetailerId: {RetailerId}", retailerId);
            throw new UnauthorizedException("No active refresh token found. Please log in again.");
        }

        // ── 4. Verify stored refresh token hash ───────────────────────────────────────
        bool tokenValid = BCrypt.Net.BCrypt.Verify(command.RefreshToken, account.RefreshTokenHash);
        if (!tokenValid)
        {
            _logger.LogWarning(
                "RefreshToken failed — hash mismatch. RetailerId: {RetailerId}", retailerId);
            throw new UnauthorizedException("The refresh token is invalid. Please log in again.");
        }

        // ── 5. Verify refresh token has not expired ────────────────────────────────────
        if (account.RefreshTokenExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogInformation(
                "RefreshToken failed — token expired. RetailerId: {RetailerId}", retailerId);
            throw new UnauthorizedException("The refresh token has expired. Please log in again.");
        }

        // ── 6. Issue new tokens (rotation) ────────────────────────────────────────────
        string newAccessToken = _tokenService.GenerateAccessToken(account);
        string newRawRefreshToken = _tokenService.GenerateRefreshToken();
        int refreshExpiryDays = account.IsRememberMeSession ? 30 : 7;
        DateTime newRefreshExpiry = DateTime.UtcNow.AddDays(refreshExpiryDays);

        string newHashedRefreshToken = BCrypt.Net.BCrypt.HashPassword(newRawRefreshToken, workFactor: 12);

        // ── 7. Persist the rotated refresh token (with concurrency guard) ──────────────
        account.UpdateRefreshToken(newHashedRefreshToken, newRefreshExpiry, account.IsRememberMeSession);
        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(account, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "RefreshToken — concurrent rotation detected. RetailerId: {RetailerId}", retailerId);
            throw new UnauthorizedException(
                "A concurrent session refresh was detected. Please retry.");
        }

        _logger.LogInformation(
            "Token rotated successfully. RetailerId: {RetailerId}. RememberMe: {RememberMe}",
            retailerId, account.IsRememberMeSession);

        // ── 8. Return new tokens ───────────────────────────────────────────────────────
        AuthTokenResponse response = account.ToAuthResponse(newAccessToken, newRawRefreshToken);
        return Result<AuthTokenResponse>.Success(response, "Tokens refreshed successfully.");
    }
}