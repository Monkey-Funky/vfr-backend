namespace Application.Features.Customer.Address.Commands.DeleteAddress;

public sealed record DeleteCustomerAddressCommand(Guid Id) : IRequest<Result<bool>>;
