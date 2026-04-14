namespace Application.Features.Customer.Address.DTOs;

public sealed record CustomerAddressDto(
    Guid Id,
    string Label,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? StateProvince,
    string PostalCode,
    string Country,
    bool IsDefault
);
