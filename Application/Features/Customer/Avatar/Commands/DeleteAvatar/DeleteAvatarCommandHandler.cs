using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Avatar.Commands.DeleteAvatar;

public sealed class DeleteAvatarCommandHandler : IRequestHandler<DeleteAvatarCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteAvatarCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(DeleteAvatarCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("Customer identity missing.");

        var avatar = await _context.Avatars
            .FirstOrDefaultAsync(a => a.Id == request.AvatarId, cancellationToken);

        if (avatar is null || avatar.CustomerId != customerId)
            throw new NotFoundException("Avatar", request.AvatarId);

        avatar.MarkAsDeleted();

        await _context.SaveChangesAsync(cancellationToken);
    }
}
