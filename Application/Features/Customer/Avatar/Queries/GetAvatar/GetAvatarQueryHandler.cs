using Application.Features.Customer.Avatar.DTOs;
using Application.Features.Customer.Avatar.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Avatar.Queries.GetAvatar;

/// <summary>
/// Returns the avatar for the given customer.
/// Cache-aside: TTL 10 minutes. Invalidated by UpdateAvatar command.
/// </summary>
public sealed class GetAvatarQueryHandler : IRequestHandler<GetAvatarQuery, AvatarDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public GetAvatarQueryHandler(
        IApplicationDbContext context,
        ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<AvatarDto> Handle(GetAvatarQuery request, CancellationToken cancellationToken)
    {
        string cacheKey = $"avatar:{request.CustomerId:N}";

        var cached = await _cacheService.GetAsync<AvatarDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var avatar = await _context.Avatars
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CustomerId == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Avatar", request.CustomerId);

        var dto = avatar.ToDto();

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), cancellationToken);

        return dto;
    }
}
