using Application.Features.Customer.Auth.DTOs;
using Application.Features.Customer.Auth.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Enums.Customer;

namespace Application.Features.Customer.Auth.Commands.Login;

public sealed class LoginCustomerCommandHandler
    : IRequestHandler<LoginCustomerCommand, Result<CustomerAuthTokenResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginCustomerCommandHandler> _logger;

    public LoginCustomerCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ILogger<LoginCustomerCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<CustomerAuthTokenResponse>> Handle(
        LoginCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var customer = await _unitOfWork.Repository<CustomerAccount>()
            .FirstOrDefaultAsync(c => c.Email == normalizedEmail && !c.IsDeleted, cancellationToken);

        if (customer is null)
        {
            _logger.LogWarning("Login failed — invalid credentials | Email: {Email}", command.Email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (customer.IsLockedOut())
        {
            _logger.LogWarning("Login attempt on locked account | Email: {Email} | LockoutEnd: {LockoutEnd}",
                command.Email, customer.LockoutUntil);
            throw new BusinessRuleException("ACCOUNT_LOCKED", $"Account is locked until {customer.LockoutUntil?.ToLocalTime():HH:mm}.");
        }

        // FIX F-04: Prevent crash on Google-only accounts where PasswordHash is null
        if (customer.PasswordHash is null)
        {
            _logger.LogWarning("Login failed — account has no password (Google-only). Email: {Email}", command.Email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        bool isValid = BCrypt.Net.BCrypt.Verify(command.Password, customer.PasswordHash);
        if (!isValid)
        {
            customer.IncrementFailedLogin();
            if (customer.IsLockedOut())
            {
                var remainingMinutes = (int)Math.Ceiling(
                (customer.LockoutUntil!.Value - DateTime.UtcNow).TotalMinutes);

                _logger.LogWarning("Account locked after failed attempts | CustomerId: {CustomerId} | Email: {Email}", customer.Id, customer.Email);

                throw new BusinessRuleException(
                "ACCOUNT_LOCKED",
                $"Your account is temporarily locked due to too many failed login attempts. " +
                $"Please try again in {remainingMinutes} minute(s).");
            }
            else
            {
                _logger.LogWarning("Login failed — invalid credentials | Email: {Email} | Attempts: {FailedAttempts}", command.Email, customer.FailedLoginAttempts);
            }
            await _unitOfWork.Repository<CustomerAccount>().UpdateAsync(customer, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("Invalid email or password.");
        }
        // PendingEmailVerification = customer started Step 1 but never completed Step 2.
        // Login is blocked until the account transitions to Active via completeProfile.
        if (customer.Status == CustomerStatus.PendingEmailVerification)
        {
            _logger.LogWarning(
                "Login failed — registration not complete. CustomerId: {CustomerId}",
                customer.Id);
            throw new BusinessRuleException(
                "EMAIL_NOT_VERIFIED",
                "Your registration is not complete. Please finish Step 2 of the registration " +
                "process. Check your inbox for the step token or start over.");
        }
        if (customer.Status is
                CustomerStatus.Suspended or
                CustomerStatus.PendingDeletion)
        {
            _logger.LogWarning(
                "Login failed — account status is {Status}. CustomerId: {CustomerId}",
                customer.Status, customer.Id);

            throw new BusinessRuleException(
                "ACCOUNT_INACTIVE",
                $"Your account is currently {customer.Status.ToString().ToLower()}. " +
                "Please contact support if you believe this is an error.");
        }

        // Success - Generate session tokens
        customer.ResetFailedLogin();

        var accessToken = _tokenService.GenerateCustomerAccessToken(customer);
        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken, workFactor: 12);

        int expirationDays = command.RememberMe ? 30 : 7;
        customer.UpdateRefreshToken(refreshTokenHash, DateTime.UtcNow.AddDays(expirationDays), command.RememberMe);

        await _unitOfWork.Repository<CustomerAccount>().UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Login successful | CustomerId: {CustomerId} | Email: {Email} | RememberMe: {RememberMe}", customer.Id, customer.Email, customer.RememberMe);

        var response = customer.ToAuthResponse(accessToken, rawRefreshToken);

        return Result<CustomerAuthTokenResponse>.Success(response, "login_success");
    }
}
