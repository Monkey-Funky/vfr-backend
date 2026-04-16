using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;

namespace Application.Features.Customer.Auth.Commands.Logout;

public sealed class LogoutCustomerCommandHandler
    : IRequestHandler<LogoutCustomerCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LogoutCustomerCommandHandler> _logger;

    public LogoutCustomerCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ITokenService tokenService,
        ILogger<LogoutCustomerCommandHandler> logger)
    {
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        LogoutCustomerCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Resolve identity
        var customerId = _currentUserService.CustomerId;
        if (!customerId.HasValue)
        {
            // Already logged out or anonymous
            var rawToken = _currentUserService.GetRawBearerToken();
            if (rawToken is not null)
            {
                ClaimsPrincipal? principal = _tokenService.GetClaimsFromExpiredToken(rawToken);

                string? subClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? principal?.FindFirst("sub")?.Value;

                if (Guid.TryParse(subClaim, out Guid idFromExpiredToken) && idFromExpiredToken != Guid.Empty)
                {
                    customerId = idFromExpiredToken;

                    _logger.LogInformation(
                        "Logout — CustomerId resolved from expired token (fallback path). " +
                        "CustomerId: {CustomerId}", customerId);
                }
            }
        }
        if (!customerId.HasValue)
        {
            _logger.LogInformation(
                "Logout called with no valid identity (no token or unverifiable token). " +
                "Returning idempotent success — treating as already logged out.");

            return Result<bool>.Success(true, "Logged out successfully.");
        }

        // 2. Clear refresh token
        var customer = await _unitOfWork.Repository<CustomerAccount>()
            .FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted, cancellationToken);

        if (customer is null || customer.IsDeleted)
        {
            _logger.LogWarning(
                "Logout — account not found or soft-deleted. CustomerId: {CustomerId}. " +
                "Returning idempotent success.", customerId);

            return Result<bool>.Success(true, "Logged out successfully.");
        }
        customer.RevokeAllRefreshTokens();

        await _unitOfWork.Repository<CustomerAccount>().UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Customer logged out. CustomerId: {CustomerId}", customerId);
        return Result<bool>.Success(true, "logout_success");
    }
}
