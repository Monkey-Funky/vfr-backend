using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;

namespace Application.Features.Customer.Address.Commands.DeleteAddress;

public sealed class DeleteCustomerAddressCommandHandler 
    : IRequestHandler<DeleteCustomerAddressCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteCustomerAddressCommandHandler> _logger;

    public DeleteCustomerAddressCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<DeleteCustomerAddressCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        DeleteCustomerAddressCommand command, 
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("User is not authenticated as a customer.");

        var address = await _unitOfWork.Repository<CustomerAddress>()
            .GetByIdAsync(command.Id, cancellationToken);

        if (address is null || address.IsDeleted || address.CustomerId != customerId)
        {
            throw new NotFoundException(nameof(CustomerAddress), command.Id);
        }

        if (address.IsDefault)
        {
            throw new BusinessRuleException("CANNOT_DELETE_DEFAULT_ADDRESS", "You cannot delete your default address. Please set another address as default first.");
        }

        await _unitOfWork.Repository<CustomerAddress>().DeleteAsync(address, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer {CustomerId} deleted address {AddressId}.", customerId, address.Id);

        return Result<bool>.Success(true, "Address deleted successfully.");
    }
}
