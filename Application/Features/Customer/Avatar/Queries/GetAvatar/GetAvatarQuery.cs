using Application.Features.Customer.Avatar.DTOs;

namespace Application.Features.Customer.Avatar.Queries.GetAvatar;

public sealed record GetAvatarQuery(Guid CustomerId) : IRequest<AvatarDto>;
