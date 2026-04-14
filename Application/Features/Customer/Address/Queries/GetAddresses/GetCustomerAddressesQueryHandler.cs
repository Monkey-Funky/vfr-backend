using Application.Features.Customer.Address.DTOs;
using Application.Features.Customer.Address.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;


namespace Application.Features.Customer.Address.Queries.GetAddresses;

public sealed class GetCustomerAddressesQueryHandler 
    : IRequestHandler<GetCustomerAddressesQuery, Result<IReadOnlyList<CustomerAddressDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetCustomerAddressesQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<CustomerAddressDto>>> Handle(
        GetCustomerAddressesQuery request, 
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("User is not authenticated as a customer.");

        var addresses = await _unitOfWork.Repository<CustomerAddress>()
            .FindAsync(a => a.CustomerId == customerId && !a.IsDeleted, cancellationToken);

        var dtos = addresses.Select(a => a.ToCustomerAddressDto()).ToList();

        return Result<IReadOnlyList<CustomerAddressDto>>.Success(dtos, "Addresses retrieved successfully.");
    }
}
