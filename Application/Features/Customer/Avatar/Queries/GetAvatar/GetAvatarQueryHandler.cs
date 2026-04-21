using Application.Features.Customer.Avatar.DTOs;
using Application.Features.Customer.Avatar.Mappings;
using Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Avatar.Queries.GetAvatar;

public sealed class GetAvatarQueryHandler : IRequestHandler<GetAvatarQuery, AvatarDto>
{
    private readonly IApplicationDbContext _context;

    public GetAvatarQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AvatarDto> Handle(GetAvatarQuery request, CancellationToken cancellationToken)
    {
        var avatar = await _context.Avatars
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CustomerId == request.CustomerId, cancellationToken);

        if (avatar is null)
            throw new NotFoundException("Avatar", request.CustomerId);

        return avatar.ToDto();
    }
}
