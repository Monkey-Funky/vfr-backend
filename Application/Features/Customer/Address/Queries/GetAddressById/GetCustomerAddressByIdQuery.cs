using Application.Features.Customer.Address.DTOs;
using MediatR;
using Shared.DTOs;

namespace Application.Features.Customer.Address.Queries.GetAddressById;

public sealed record GetCustomerAddressByIdQuery(Guid Id) : IRequest<Result<CustomerAddressDto>>;
