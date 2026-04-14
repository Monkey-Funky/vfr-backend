using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Enums.Customer;
using System.Security.Principal;

namespace Application.Features.Customer.Auth.Commands.ResetPassword;

public sealed class ResetPasswordCustomerCommandHandler
    : IRequestHandler<ResetPasswordCustomerCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ResetPasswordCustomerCommandHandler> _logger;

    public ResetPasswordCustomerCommandHandler(
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<ResetPasswordCustomerCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        ResetPasswordCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        var customer = await _unitOfWork.Repository<CustomerAccount>()
            .FirstOrDefaultAsync(c => c.Email == normalizedEmail &&
            !c.IsDeleted && 
            c.Status == CustomerStatus.Active,
            cancellationToken);
        if (customer is null)
        {
            _logger.LogWarning(
                "ResetPassword — email not found or account inactive. Email: {Email}",
                command.Email);

            throw new BusinessRuleException(
                "INVALID_OTP",
                "The OTP code is invalid or has expired. Please request a new one.");
        }

        var cacheKey = $"customer_pwd_reset:{normalizedEmail}";

        // Verify OTP
        var cachedOtp = await _cacheService.GetAsync<string>(cacheKey, cancellationToken);
        if (cachedOtp is null)
        {
            _logger.LogWarning(
                "ResetPassword — OTP not found in cache (expired?). CustomerId: {CustomerId}",
                customer.Id);

            throw new BusinessRuleException(
                "INVALID_OTP",
                "The OTP code is invalid or has expired. Please request a new one.");
        }
        bool otpValid = BCrypt.Net.BCrypt.Verify(command.OtpCode, cachedOtp);

        if (!otpValid)
        {
            _logger.LogWarning(
                "ResetPassword — OTP mismatch. CustomerId: {CustomerId}", customer.Id);

            throw new BusinessRuleException(
                "INVALID_OTP",
                "The OTP code is incorrect. Please check your email and try again.");
        }

        // 3. Update password and revoke sessions
        string newPasswordHash = BCrypt.Net.BCrypt.HashPassword(command.NewPassword, workFactor: 12);
        customer.ResetPassword(newPasswordHash);
        customer.RevokeAllRefreshTokens();

        // 4. Cleanup
        await _cacheService.RemoveAsync(cacheKey, cancellationToken);
        await _unitOfWork.Repository<CustomerAccount>().UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset successfully for CustomerId: {CustomerId}", customer.Id);

        return Result<bool>
            .Success(true, "Password has been reset successfully. Please log in with your new password.");
    }
}
