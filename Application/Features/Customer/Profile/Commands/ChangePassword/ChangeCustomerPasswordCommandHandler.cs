using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Enums.Customer;

namespace Application.Features.Customer.Profile.Commands.ChangePassword;

public sealed class ChangeCustomerPasswordCommandHandler 
    : IRequestHandler<ChangeCustomerPasswordCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ChangeCustomerPasswordCommandHandler> _logger;

    public ChangeCustomerPasswordCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<ChangeCustomerPasswordCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        ChangeCustomerPasswordCommand command, 
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("User is not authenticated as a customer.");

        var customer = await _unitOfWork.Repository<CustomerAccount>()
            .GetByIdAsync(customerId, cancellationToken);

        if (customer is null || customer.IsDeleted)
            throw new NotFoundException(nameof(CustomerAccount), customerId);

        if (customer.Status is not CustomerStatus.Active)
        {
            throw new BusinessRuleException("ACCOUNT_INACTIVE",
                $"Password changes are not allowed. Account status: {customer.Status}.");
        }

        if (customer.PasswordHash is null)
        {
            throw new BusinessRuleException("NO_PASSWORD", "Cannot change password for an account that uses social login only.");
        }

        bool isCurrentValid = BCrypt.Net.BCrypt.Verify(command.CurrentPassword, customer.PasswordHash);
        if (!isCurrentValid)
        {
            throw new BusinessRuleException("INVALID_PASSWORD", "The current password provided is incorrect.");
        }

        string newPasswordHash = BCrypt.Net.BCrypt.HashPassword(command.NewPassword, workFactor: 12);
        
        customer.ResetPassword(newPasswordHash);
        customer.RevokeAllRefreshTokens();

        await _unitOfWork.Repository<CustomerAccount>().UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer {CustomerId} changed their password successfully.", customerId);

        return Result<bool>.Success(true, "Password changed successfully. You have been optionally logged out of other devices.");
    }
}
