using Application.Features.Customer.Auth.DTOs;
using Domain.Entities.Customer;

namespace Application.Features.Customer.Auth.Mappings;

public static class CustomerMappings
{
    public static CustomerAuthTokenResponse ToAuthResponse
        (this CustomerAccount customer, string accessToken, string rawRefreshToken) => new(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            ExpiresIn: 15 * 60,               // 900 seconds = 15 minutes
            CustomerProfile: customer.ToProfileDto()
        );

    public static CustomerProfileDto ToProfileDto(this CustomerAccount customer)
    {
        return new CustomerProfileDto(
            customer.Id,
            customer.FullName,
            customer.Email,
            customer.PhoneNumber,
            customer.DateOfBirth,
            customer.Gender,
            customer.AvatarUrl,
            customer.Status);
    }
}
