using Application.Features.Customer.Auth.DTOs;
using Application.Features.Customer.Auth.Mappings;
using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Enums.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Auth.Commands.LoginWithGoogle;

public sealed class LoginWithGoogleCustomerCommandHandler
    : IRequestHandler<LoginWithGoogleCustomerCommand, Result<CustomerAuthTokenResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginWithGoogleCustomerCommandHandler> _logger;

    public LoginWithGoogleCustomerCommandHandler(
        IUnitOfWork unitOfWork,
        IGoogleAuthService googleAuthService,
        ITokenService tokenService,
        ILogger<LoginWithGoogleCustomerCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _googleAuthService = googleAuthService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<CustomerAuthTokenResponse>> Handle(
        LoginWithGoogleCustomerCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Validate Google ID token
        GoogleUserInfo googleUser = await _googleAuthService
            .ValidateAsync(command.GoogleIdToken, cancellationToken);

        _logger.LogInformation("Google token validated for customer. Email: {Email}, GoogleId: {GoogleId}", 
            googleUser.Email, googleUser.GoogleId);

        // 2. Find existing customer
        var customer = await _unitOfWork
            .Repository<CustomerAccount>()
            .FirstOrDefaultAsync(c => 
            (c.GoogleId == googleUser.GoogleId || 
            c.Email.ToLower() == googleUser.Email.ToLower()) && 
            !c.IsDeleted, cancellationToken);

        bool isNewAccount = false;

        if (customer is not null)
        {
            // Block access for non-Active account statuses (excluding PendingEmailVerification for Google login which auto-activates)
            if (customer.Status is CustomerStatus.Suspended or
                CustomerStatus.PendingDeletion)
            {
                string message = $"Your account is currently {customer.Status.ToString().ToLower()}. " +
                                 "Please contact support for assistance.";

                _logger.LogWarning("Google login blocked for customer — account status is {Status}. CustomerId: {CustomerId}", 
                    customer.Status, customer.Id);
                throw new BusinessRuleException("ACCOUNT_INACTIVE", message);
            }

            // Link GoogleId if not set
            if (customer.GoogleId is null)
            {
                customer.SetGoogleId(googleUser.GoogleId);
                
                // FIX F-05: Google login verifies email automatically. 
                // Transition PendingEmailVerification -> Active.
                if (customer.Status == CustomerStatus.PendingEmailVerification)
                {
                    customer.MarkEmailVerified();
                }
                _logger.LogInformation("Linked Google account to existing customer. CustomerId: {CustomerId}, NewStatus: {Status}", customer.Id, customer.Status);
            }
        }
        else
        {
            // 3. New account
            customer = CustomerAccount.CreateWithGoogle(
                googleUser.FullName, googleUser.Email, googleUser.GoogleId);
            isNewAccount = true;
        }

        // 4. Issue tokens
        string accessToken = _tokenService.GenerateCustomerAccessToken(customer);
        string rawRefreshToken = _tokenService.GenerateRefreshToken();
        string hashedRefreshToken = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken, workFactor: 12);

        customer.UpdateRefreshToken(hashedRefreshToken, DateTime.UtcNow.AddDays(7), false);

        // 5. Persist
        if (isNewAccount)
        {
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await _unitOfWork.Repository<CustomerAccount>()
                        .AddAsync(customer, ct);

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
                    "An account with this email or already exists. " +
                    "If you already have an account, please sign in using your email and password.");
            }

            _logger.LogInformation(
                "New customer created via Google OAuth with NotificationPreference seeded. " +
                "Email: {Email}. CustomerId: {CustomerId}",
                googleUser.Email, customer.Id);
        }
        else
        {
            // Existing account — update the refresh token (and optionally the linked GoogleId).
            // No transaction needed for a single-row update.
            try
            {
                await _unitOfWork.Repository<CustomerAccount>()
                    .UpdateAsync(customer, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _logger.LogWarning(ex,
                    "LoginWithGoogle — unexpected unique constraint violation on existing account update. " +
                    "CustomerId: {CustomerId}", customer.Id);

                // This should be extremely rare for an existing account update,
                // but we guard it anyway to prevent unhandled 500.
                throw new BusinessRuleException(
                    "ACCOUNT_CONFLICT",
                    "A conflict occurred while updating your account. Please try again.");
            }
        }

        _logger.LogInformation("Customer Google login successful. CustomerId: {CustomerId}, NewAccount: {IsNew}", 
            customer.Id, isNewAccount);

        // 6. Return response
        var response = customer.ToAuthResponse(accessToken, rawRefreshToken);
        return Result<CustomerAuthTokenResponse>.Success(response, "google_login_success");
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
