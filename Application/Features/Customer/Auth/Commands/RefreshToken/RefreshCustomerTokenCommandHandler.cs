using Application.Features.Customer.Auth.DTOs;
using Application.Features.Customer.Auth.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Enums.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Auth.Commands.RefreshToken;

public sealed class RefreshCustomerTokenCommandHandler
    : IRequestHandler<RefreshCustomerTokenCommand, Result<CustomerAuthTokenResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RefreshCustomerTokenCommandHandler> _logger;

    public RefreshCustomerTokenCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<RefreshCustomerTokenCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<CustomerAuthTokenResponse>> Handle(
        RefreshCustomerTokenCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Extract CustomerId from the (possibly expired) access token without lifetime check
        ClaimsPrincipal? principal = _tokenService.GetClaimsFromExpiredToken(command.AccessToken);

        if (principal is null)
        {
            _logger.LogWarning("RefreshCustomerToken failed — access token is structurally invalid.");
            // AuthenticationException → HTTP 401; UnauthorizedException is for IDOR / 403.
            throw new AuthenticationException("The access token is invalid.");
        }

        string? subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? principal.FindFirst("sub")?.Value;

        if (!Guid.TryParse(subClaim, out Guid customerId) || customerId == Guid.Empty)
        {
            _logger.LogWarning("RefreshCustomerToken failed — 'sub' claim is missing or invalid.");
            throw new AuthenticationException("The access token does not contain a valid identity.");
        }

        // FIX F-02: Validate role claim to prevent cross-role attacks
        string? roleClaim = principal.FindFirst("role")?.Value
                         ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (roleClaim != "Customer")
        {
            _logger.LogWarning("RefreshCustomerToken — role claim is '{Role}', expected 'Customer'.", roleClaim);
            throw new AuthenticationException("The access token is not a valid Customer token.");
        }

        // 2. Load the CustomerAccount
        var customer = await _unitOfWork.Repository<CustomerAccount>()
            .GetByIdAsync(customerId, cancellationToken);

        if (customer is null || customer.IsDeleted)
        {
            _logger.LogWarning(
                "RefreshCustomerToken failed — account not found or deleted. CustomerId: {CustomerId}", customerId);
            throw new AuthenticationException("Account not found.");
        }

        // Guard: account must be Active
        if (customer.Status != CustomerStatus.Active)
        {
            _logger.LogWarning(
                "RefreshCustomerToken failed — account status is {Status}. CustomerId: {CustomerId}",
                customer.Status, customerId);
            throw new AuthenticationException("Your account is no longer active. Please log in again.");
        }

        // 3. Verify RefreshToken matches hash using EnhancedVerify as requested
        if (customer.RefreshTokenHash is null || customer.RefreshTokenExpiresAt is null)
        {
            _logger.LogWarning(
                "RefreshCustomerToken failed — no active refresh token. CustomerId: {CustomerId}", customerId);
            throw new AuthenticationException("No active refresh token found. Please log in again.");
        }

        bool isTokenValid = BCrypt.Net.BCrypt.EnhancedVerify(command.RefreshToken, customer.RefreshTokenHash);
        if (!isTokenValid)
        {
            _logger.LogWarning(
                "RefreshCustomerToken failed — hash mismatch. CustomerId: {CustomerId}", customerId);
            throw new AuthenticationException("The refresh token is invalid. Please log in again.");
        }

        // 4. Verify refresh token has not expired
        if (customer.RefreshTokenExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogInformation("RefreshCustomerToken failed — token expired. CustomerId: {CustomerId}", customerId);
            throw new AuthenticationException("The refresh token has expired. Please log in again.");
        }

        // 5. Generate new access + refresh tokens (Rotation)
        string newAccessToken = _tokenService.GenerateCustomerAccessToken(customer);
        string newRawRefreshToken = _tokenService.GenerateRefreshToken();

        // Preserve RememberMe TTL (30 days vs 7 days)
        int refreshExpiryDays = customer.RememberMe ? 30 : 7;
        DateTime newRefreshExpiry = DateTime.UtcNow.AddDays(refreshExpiryDays);

        string newHashedRefreshToken = BCrypt.Net.BCrypt.HashPassword(newRawRefreshToken, workFactor: 12);

        // 6. Update account
        customer.UpdateRefreshToken(newHashedRefreshToken, newRefreshExpiry, customer.RememberMe);
        await _unitOfWork.Repository<CustomerAccount>().UpdateAsync(customer, cancellationToken);

        // 7. Persist with concurrency guard
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "RefreshCustomerToken — concurrent rotation detected. CustomerId: {CustomerId}", customerId);
            // Concurrent rotation means a second device/request beat us to it.
            // The caller must re-authenticate — this is an authentication flow failure.
            throw new AuthenticationException("A concurrent session refresh was detected. Please retry.");
        }

        _logger.LogInformation(
            "Customer token rotated successfully. CustomerId: {CustomerId}. RememberMe: {RememberMe}",
            customerId, customer.RememberMe);

        // 8. Return response
        var response = customer.ToAuthResponse(newAccessToken, newRawRefreshToken);
        return Result<CustomerAuthTokenResponse>.Success(response, "Token refreshed successfully.");
    }
}