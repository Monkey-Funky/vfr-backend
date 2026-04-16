namespace Application.Features.Customer.Address.Commands.SetDefaultAddress;

public sealed record SetDefaultAddressCommand(Guid Id) : IRequest<Result<bool>>;
