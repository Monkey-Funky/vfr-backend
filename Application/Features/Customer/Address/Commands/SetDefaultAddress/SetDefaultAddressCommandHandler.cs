using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Shared.Constants;

namespace Application.Features.Customer.Address.Commands.SetDefaultAddress;

public sealed class SetDefaultAddressCommandHandler
    : IRequestHandler<SetDefaultAddressCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<SetDefaultAddressCommandHandler> _logger;

    public SetDefaultAddressCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        ILogger<SetDefaultAddressCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
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
            throw new NotFoundException(nameof(CustomerAddress), command.Id);

        // Idempotency: already the default — nothing to change.
        if (newDefaultAddress.IsDefault)
            return Result<bool>.Success(true, "Address is already set as default.");

        // Atomically unset the previous default and set the new one.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var previousDefault = await _unitOfWork.Repository<CustomerAddress>()
                .FirstOrDefaultAsync(
                    a => a.CustomerId == customerId && a.IsDefault && !a.IsDeleted, ct);

            if (previousDefault is not null)
            {
                previousDefault.UnsetDefault();
                await _unitOfWork.Repository<CustomerAddress>().UpdateAsync(previousDefault, ct);
            }

            newDefaultAddress.SetAsDefault();
            await _unitOfWork.Repository<CustomerAddress>().UpdateAsync(newDefaultAddress, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        _logger.LogInformation(
            "Customer {CustomerId} set address {AddressId} as default.", customerId, newDefaultAddress.Id);

        // Invalidate cached address list.
        await _cacheService.RemoveAsync(CacheKeys.CustomerAddresses(customerId), cancellationToken);

        return Result<bool>.Success(true, "Address set as default successfully.");
    }
}
