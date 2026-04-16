using Application.Features.Customer.Address.DTOs;

namespace Application.Features.Customer.Address.Commands.UpdateAddress;

public sealed record UpdateCustomerAddressCommand(
    Guid Id,
    string Label,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? StateProvince,
    string PostalCode,
    string Country
) : IRequest<Result<CustomerAddressDto>>;
