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
    private readonly ILogger<DeleteCustomerAccountCommandHandler> _logger;

    public DeleteCustomerAccountCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<DeleteCustomerAccountCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
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

        if (customer.Status == CustomerStatus.PendingDeletion)
        {
            return Result<bool>.Success(true, "Your account is already marked for deletion.");
        }

        customer.SetPendingDeletion();
        customer.RevokeAllRefreshTokens();

        await _unitOfWork.Repository<CustomerAccount>().UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer {CustomerId} requested account deletion. Status set to PendingDeletion.", customerId);

        return Result<bool>.Success(true, "Your account has been marked for deletion. This action will be processed shortly.");
    }
}
