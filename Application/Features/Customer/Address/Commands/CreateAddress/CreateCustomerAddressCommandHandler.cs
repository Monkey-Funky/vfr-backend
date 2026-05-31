using Application.Features.Customer.Address.DTOs;
using Application.Features.Customer.Address.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Shared.Constants;

namespace Application.Features.Customer.Address.Commands.CreateAddress;

public sealed class CreateCustomerAddressCommandHandler
    : IRequestHandler<CreateCustomerAddressCommand, Result<CustomerAddressDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CreateCustomerAddressCommandHandler> _logger;

    public CreateCustomerAddressCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        ILogger<CreateCustomerAddressCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<CustomerAddressDto>> Handle(
        CreateCustomerAddressCommand command,
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("User is not authenticated as a customer.");

        const int MaxAddressesPerCustomer = 10;
        var existingCount = await _unitOfWork.Repository<CustomerAddress>()
            .CountAsync(a => a.CustomerId == customerId && !a.IsDeleted, cancellationToken);

        if (existingCount >= MaxAddressesPerCustomer)
            throw new BusinessRuleException(
                "ADDRESS_LIMIT_REACHED",
                $"You cannot have more than {MaxAddressesPerCustomer} addresses.");

        var address = CustomerAddress.Create(
            customerId,
            command.Label,
            command.AddressLine1,
            command.AddressLine2,
            command.City,
            command.StateProvince,
            command.PostalCode,
            command.Country,
            command.IsDefault);

        if (command.IsDefault)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var previousDefault = await _unitOfWork.Repository<CustomerAddress>()
                    .FirstOrDefaultAsync(a => a.CustomerId == customerId && a.IsDefault && !a.IsDeleted, ct);

                if (previousDefault is not null)
                {
                    previousDefault.UnsetDefault();
                    await _unitOfWork.Repository<CustomerAddress>().UpdateAsync(previousDefault, ct);
                }

                await _unitOfWork.Repository<CustomerAddress>().AddAsync(address, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        else
        {
            await _unitOfWork.Repository<CustomerAddress>().AddAsync(address, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Customer {CustomerId} created address {AddressId}.", customerId, address.Id);

        // Invalidate the cached addresses list so the next read reflects the new entry.
        await _cacheService.RemoveAsync(CacheKeys.CustomerAddresses(customerId), cancellationToken);

        return Result<CustomerAddressDto>.Success(address.ToCustomerAddressDto(), "Address created successfully.");
    }
}
