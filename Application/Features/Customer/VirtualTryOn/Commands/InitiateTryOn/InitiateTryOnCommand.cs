using Application.Features.Customer.VirtualTryOn.DTOs;
using Domain.Enums.Customer;
using MediatR;

namespace Application.Features.Customer.VirtualTryOn.Commands.InitiateTryOn;

// Note: Rate Limiting [EnableRateLimiting("customer-tryon")] will be applied
// to the controller endpoint in CP-015 to protect this compute-heavy route.
public sealed record InitiateTryOnCommand(
    Guid ProductId, 
    TryOnSessionType SessionType, 
    Guid? AvatarId) : IRequest<TryOnResultDto>;
