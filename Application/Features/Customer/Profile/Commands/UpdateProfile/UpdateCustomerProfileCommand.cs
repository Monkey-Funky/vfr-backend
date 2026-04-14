using Application.Features.Customer.Auth.DTOs;

namespace Application.Features.Customer.Profile.Commands.UpdateProfile;

public sealed record UpdateCustomerProfileCommand(
    string FullName,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? Gender
) : IRequest<Result<CustomerProfileDto>>;
