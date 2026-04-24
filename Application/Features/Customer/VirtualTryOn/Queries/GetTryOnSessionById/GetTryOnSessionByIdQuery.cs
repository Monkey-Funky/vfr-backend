using Application.Features.Customer.VirtualTryOn.DTOs;

namespace Application.Features.Customer.VirtualTryOn.Queries.GetTryOnSessionById;

public sealed record GetTryOnSessionByIdQuery(Guid SessionId) : IRequest<VirtualTryOnSessionDto>;
