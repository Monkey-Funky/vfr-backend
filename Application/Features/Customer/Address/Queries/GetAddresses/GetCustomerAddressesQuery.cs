using Application.Features.Customer.Address.DTOs;

namespace Application.Features.Customer.Address.Queries.GetAddresses;

public sealed record GetCustomerAddressesQuery : IRequest<Result<IReadOnlyList<CustomerAddressDto>>>;
