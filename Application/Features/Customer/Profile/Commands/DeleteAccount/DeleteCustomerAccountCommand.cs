namespace Application.Features.Customer.Profile.Commands.DeleteAccount;

public sealed record DeleteCustomerAccountCommand(string Password) : IRequest<Result<bool>>;
