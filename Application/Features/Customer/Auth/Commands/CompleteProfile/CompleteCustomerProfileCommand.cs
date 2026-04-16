using Application.Features.Customer.Auth.DTOs;

namespace Application.Features.Customer.Auth.Commands.CompleteProfile;

public sealed record CompleteCustomerProfileCommand(
    string? Gender,
    DateOnly DateOfBirth,
    string? PhoneNumber,
    string TempStepToken
) : IRequest<Result<CustomerAuthTokenResponse>>;
