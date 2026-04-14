using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Enums.Customer;
using System.Security.Cryptography;

namespace Application.Features.Customer.Auth.Commands.ForgotPassword;

public sealed class ForgotPasswordCustomerCommandHandler
    : IRequestHandler<ForgotPasswordCustomerCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ForgotPasswordCustomerCommandHandler> _logger;

    public ForgotPasswordCustomerCommandHandler(
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IEmailService emailService,
        ILogger<ForgotPasswordCustomerCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        ForgotPasswordCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        // 1. Find account
        var customer = await _unitOfWork.Repository<CustomerAccount>()
            .FirstOrDefaultAsync(c => c.Email == normalizedEmail && !c.IsDeleted, cancellationToken);

        // 2. Security Rule: Always return success to prevent email enumeration
        if (customer == null || customer.Status == CustomerStatus.PendingDeletion)
        {
            _ = BCrypt.Net.BCrypt.HashPassword(command.Email, workFactor: 4);
            _logger.LogInformation
                ("Forgot password attempt for non-existent or deleted email: {Email}", command.Email);

            return Result<bool>.Success(true, "If an account exists for this email, a reset code has been sent.");
        }

        // 3. Generate 6-digit OTP
        var otp = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();

        string hashedOtp = BCrypt.Net.BCrypt.HashPassword(otp, workFactor: 12);

        // 4. Store in cache (15-minute TTL)
        var cacheKey = $"customer_pwd_reset:{normalizedEmail}";
        await _cacheService.SetAsync(cacheKey, hashedOtp, TimeSpan.FromMinutes(10), cancellationToken);

        // 5. Send email
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendEmailAsync(
                    customer.Email,
                    "Reset your Virtual Fitting Room password",
                    $"<p>Your password reset code is: <strong>{otp}</strong></p><p>This code will expire in 10 minutes.</p>",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to CustomerId: {CustomerId}", customer.Id);
            }
        });

        _logger.LogInformation("Password reset OTP generated. CustomerId: {CustomerId}", customer.Id);

        return Result<bool>.Success(true, "If an account exists for this email, a reset code has been sent.");
    }
}
