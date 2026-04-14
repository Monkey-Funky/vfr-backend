using Application.Features.Customer.Address.DTOs;
using Application.Features.Customer.Address.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Exceptions;
using MediatR;
using Shared.DTOs;

namespace Application.Features.Customer.Address.Queries.GetAddressById;

public sealed class GetCustomerAddressByIdQueryHandler 
    : IRequestHandler<GetCustomerAddressByIdQuery, Result<CustomerAddressDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetCustomerAddressByIdQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CustomerAddressDto>> Handle(
        GetCustomerAddressByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("User is not authenticated as a customer.");

        var address = await _unitOfWork.Repository<CustomerAddress>()
            .GetByIdAsync(request.Id, cancellationToken);

        if (address is null || address.IsDeleted || address.CustomerId != customerId)
        {
            throw new NotFoundException(nameof(CustomerAddress), request.Id);
        }

        return Result<CustomerAddressDto>.Success(address.ToCustomerAddressDto(), "Address retrieved successfully.");
    }
}
