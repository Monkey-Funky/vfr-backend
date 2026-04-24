using Application.Features.Customer.Avatar.DTOs;
using Application.Features.Customer.Avatar.Mappings;
using Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Avatar.Queries.GetAvatarHistory;

public sealed class GetAvatarMeasurementHistoryQueryHandler 
    : IRequestHandler<GetAvatarMeasurementHistoryQuery, PagedResult<AvatarMeasurementHistoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAvatarMeasurementHistoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<AvatarMeasurementHistoryDto>> Handle(
        GetAvatarMeasurementHistoryQuery request, 
        CancellationToken cancellationToken)
    {
        var avatarId = await _context.Avatars
            .IgnoreQueryFilters()
            .Where(a => a.CustomerId == request.CustomerId)
            .Select(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (avatarId == Guid.Empty)
            throw new NotFoundException("Avatar", request.CustomerId);

        var query = _context.AvatarMeasurementHistory
            .AsNoTracking()
            .Where(h => h.AvatarId == avatarId)
            .OrderByDescending(h => h.RecordedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(h => h.ToDto()).ToList();

        return new PagedResult<AvatarMeasurementHistoryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
