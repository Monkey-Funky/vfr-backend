using Application.Features.Customer.Auth.DTOs;

namespace Application.Features.Customer.Auth.Commands.Login;

public sealed record LoginCustomerCommand(
    string Email,
    string Password,
    bool RememberMe = false
) : IRequest<Result<CustomerAuthTokenResponse>>;
