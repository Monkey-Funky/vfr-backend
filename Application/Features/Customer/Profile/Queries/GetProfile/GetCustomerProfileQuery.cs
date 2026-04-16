using Application.Features.Customer.Auth.DTOs;

namespace Application.Features.Customer.Profile.Queries.GetProfile;

public sealed record GetCustomerProfileQuery : IRequest<Result<CustomerProfileDto>>;
