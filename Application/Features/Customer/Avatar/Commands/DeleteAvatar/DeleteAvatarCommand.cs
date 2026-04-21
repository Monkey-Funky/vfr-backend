namespace Application.Features.Customer.Avatar.Commands.DeleteAvatar;

public sealed record DeleteAvatarCommand(Guid AvatarId) : IRequest;
