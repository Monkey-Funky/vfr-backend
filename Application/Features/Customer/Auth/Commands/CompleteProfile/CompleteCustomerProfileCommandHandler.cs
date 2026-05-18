using Application.Features.Customer.Auth.DTOs;
using Application.Features.Customer.Auth.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Enums.Customer;


namespace Application.Features.Customer.Auth.Commands.CompleteProfile;

public sealed class CompleteCustomerProfileCommandHandler
    : IRequestHandler<CompleteCustomerProfileCommand, Result<CustomerAuthTokenResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly ILogger<CompleteCustomerProfileCommandHandler> _logger;

    public CompleteCustomerProfileCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        IEmailService emailService,
        ILogger<CompleteCustomerProfileCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<CustomerAuthTokenResponse>> Handle(CompleteCustomerProfileCommand command, CancellationToken cancellationToken)
    {
        ClaimsPrincipal? principal = _tokenService.ValidateTempStepToken(command.TempStepToken);
        if (principal is null || principal.FindFirst("token_type")?.Value != "step" || principal.FindFirst("step")?.Value != "1")
        {
            // AuthenticationException → HTTP 401: the caller cannot prove their identity
            // via the registration step token. UnauthorizedException is for IDOR / 403.
            throw new AuthenticationException("The registration step token is invalid or has expired.");
        }

        Guid tempAccountId = Guid.Parse(principal.FindFirst("temp_account_id")!.Value);
        var account = await _unitOfWork.Repository<CustomerAccount>().GetByIdAsync(tempAccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerAccount), tempAccountId);

        if (account.Status != CustomerStatus.PendingEmailVerification)
            throw new BusinessRuleException("REGISTRATION_ALREADY_COMPLETED", "This registration has already been completed.");

        account.CompleteProfile(command.Gender, command.DateOfBirth, command.PhoneNumber);
        account.MarkEmailVerified(); // FIX F-01: Transition status to Active so login is no longer blocked

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _unitOfWork.Repository<CustomerAccount>().UpdateAsync(account, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        catch
        {
            throw;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendEmailAsync(
                    account.Email,
                    "Welcome to VFR!",
                    "Your account is active.",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Welcome email failed for CustomerId: {CustomerId}", account.Id);
            }
        });

        string accessToken = _tokenService.GenerateCustomerAccessToken(account);
        string rawRefreshToken = _tokenService.GenerateRefreshToken();
        string hashedRefreshToken = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken, workFactor: 12);

        account.UpdateRefreshToken(hashedRefreshToken, DateTime.UtcNow.AddDays(7), false);
        await _unitOfWork.Repository<CustomerAccount>().UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registration Step 2 complete. CustomerId: {CustomerId}. Email: {Email}", account.Id, account.Email);

        var response = account.ToAuthResponse(accessToken, rawRefreshToken);

        return Result<CustomerAuthTokenResponse>.Success(response, "Registration complete. Welcome!");
    }
}