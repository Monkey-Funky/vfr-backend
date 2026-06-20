using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Enums.Customer;

namespace Application.Features.Customer.Profile.Commands.DeleteAccount;

public sealed class DeleteCustomerAccountCommandHandler
    : IRequestHandler<DeleteCustomerAccountCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<DeleteCustomerAccountCommandHandler> _logger;

    public DeleteCustomerAccountCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        ILogger<DeleteCustomerAccountCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        DeleteCustomerAccountCommand command,
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("User is not authenticated as a customer.");

        var customer = await _unitOfWork.Repository<CustomerAccount>()
            .GetByIdAsync(customerId, cancellationToken);

        if (customer is null || customer.IsDeleted)
            throw new NotFoundException(nameof(CustomerAccount), customerId);

        // Verify the password before allowing deletion.
        if (customer.PasswordHash is null)
        {
            throw new BusinessRuleException(
                "NO_PASSWORD",
                "Cannot delete an account that uses social login only. Please disconnect your social account from the provider instead.");
        }

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(command.Password, customer.PasswordHash);
        if (!isPasswordValid)
        {
            throw new BusinessRuleException(
                "INVALID_PASSWORD",
                "The password provided is incorrect. Account deletion was not performed.");
        }

        // Idempotency: already marked for deletion — nothing to do.
        if (customer.Status == CustomerStatus.PendingDeletion)
        {
            await _cacheService.RemoveAsync($"cust_profile:{customerId:N}", cancellationToken);
            return Result<bool>.Success(true, "Your account is already marked for deletion.");
        }

        customer.SetPendingDeletion();
        customer.RevokeAllRefreshTokens();

        await _unitOfWork.Repository<CustomerAccount>().UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Customer {CustomerId} confirmed account deletion with correct password.", customerId);

        await _cacheService.RemoveAsync($"cust_profile:{customerId:N}", cancellationToken);

        return Result<bool>.Success(
            true,
            "Your account has been marked for deletion. This action will be processed shortly.");
    }
}
