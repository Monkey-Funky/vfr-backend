using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;

namespace Application.Features.Customer.Address.Commands.SetDefaultAddress;

public sealed class SetDefaultAddressCommandHandler 
    : IRequestHandler<SetDefaultAddressCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SetDefaultAddressCommandHandler> _logger;

    public SetDefaultAddressCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<SetDefaultAddressCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        SetDefaultAddressCommand command, 
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("User is not authenticated as a customer.");

        var newDefaultAddress = await _unitOfWork.Repository<CustomerAddress>()
            .GetByIdAsync(command.Id, cancellationToken);

        if (newDefaultAddress is null || newDefaultAddress.IsDeleted || newDefaultAddress.CustomerId != customerId)
        {
            throw new NotFoundException(nameof(CustomerAddress), command.Id);
        }

        if (newDefaultAddress.IsDefault)
        {
            return Result<bool>.Success(true, "Address is already set as default.");
        }

        // Requires atomic transaction to unset previous default
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var previousDefault = await _unitOfWork.Repository<CustomerAddress>()
                .FirstOrDefaultAsync(a => a.CustomerId == customerId && a.IsDefault && !a.IsDeleted, ct);

            if (previousDefault is not null)
            {
                previousDefault.UnsetDefault();
                await _unitOfWork.Repository<CustomerAddress>().UpdateAsync(previousDefault, ct);
            }

            newDefaultAddress.SetAsDefault();
            await _unitOfWork.Repository<CustomerAddress>().UpdateAsync(newDefaultAddress, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        _logger.LogInformation("Customer {CustomerId} set address {AddressId} as default.", customerId, newDefaultAddress.Id);

        return Result<bool>.Success(true, "Default address updated successfully.");
    }
}
