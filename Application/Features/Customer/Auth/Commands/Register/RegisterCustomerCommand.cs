namespace Application.Features.Customer.Auth.Commands.Register;

public sealed record RegisterCustomerCommand(
    string FullName,
    string Email,
    string Password
) : IRequest<Result<string>>;

