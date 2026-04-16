namespace Application.Features.Customer.Profile.Commands.DeleteAccount;

public sealed record DeleteCustomerAccountCommand : IRequest<Result<bool>>;
