using Application.Features.Customer.Address.DTOs;
using Domain.Entities.Customer;

namespace Application.Features.Customer.Address.Mappings;

public static class CustomerAddressMappings
{
    public static CustomerAddressDto ToCustomerAddressDto(this CustomerAddress address)
    {
        return new CustomerAddressDto(
            address.Id,
            address.Label,
            address.AddressLine1,
            address.AddressLine2,
            address.City,
            address.StateProvince,
            address.PostalCode,
            address.Country,
            address.IsDefault
        );
    }
}
