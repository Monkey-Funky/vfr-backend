using Application.Features.Customer.Address.DTOs;

namespace Application.Features.Customer.Address.Commands.CreateAddress;

public sealed record CreateCustomerAddressCommand(
    string Label,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? StateProvince,
    string PostalCode,
    string Country,
    bool IsDefault
) : IRequest<Result<CustomerAddressDto>>;
