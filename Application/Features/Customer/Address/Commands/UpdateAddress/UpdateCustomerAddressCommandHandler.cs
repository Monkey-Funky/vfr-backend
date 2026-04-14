using Application.Features.Customer.Address.DTOs;
using Application.Features.Customer.Address.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;

namespace Application.Features.Customer.Address.Commands.UpdateAddress;

public sealed class UpdateCustomerAddressCommandHandler 
    : IRequestHandler<UpdateCustomerAddressCommand, Result<CustomerAddressDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateCustomerAddressCommandHandler> _logger;

    public UpdateCustomerAddressCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<UpdateCustomerAddressCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<CustomerAddressDto>> Handle(
        UpdateCustomerAddressCommand command, 
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

        address.Update(
            command.Label,
            command.AddressLine1,
            command.AddressLine2,
            command.City,
            command.StateProvince,
            command.PostalCode,
            command.Country
        );

        await _unitOfWork.Repository<CustomerAddress>().UpdateAsync(address, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer {CustomerId} updated address {AddressId}.", customerId, address.Id);

        return Result<CustomerAddressDto>.Success(address.ToCustomerAddressDto(), "Address updated successfully.");
    }
}
