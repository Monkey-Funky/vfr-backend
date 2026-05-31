using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Application.Features.Customer.Outfits.DTOs;
using Application.Features.Customer.Outfits.Mappings;

namespace Application.Features.Customer.Outfits.Queries.GetOutfits;

/// <summary>
/// Returns all outfits for the authenticated customer.
/// Cache-aside: TTL 5 minutes. Invalidated by CreateOutfit, UpdateOutfit, DeleteOutfit.
/// </summary>
internal sealed class GetOutfitsQueryHandler : IRequestHandler<GetOutfitsQuery, PagedResult<OutfitSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetOutfitsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<OutfitSummaryDto>> Handle(GetOutfitsQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can view outfits.");

        string cacheKey = $"outfits:{customerId:N}";

        var cached = await _cacheService.GetAsync<PagedResult<OutfitSummaryDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var outfits = await _context.CustomerOutfits
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var productIds = outfits
            .SelectMany(o => o.Items)
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();

        var productsImages = productIds.Count > 0
            ? await _context.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    PrimaryImage = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault()
                })
                .ToDictionaryAsync(p => p.Id, p => p.PrimaryImage, cancellationToken)
            : new Dictionary<Guid, string?>();

        var items = outfits.Select(o => o.ToOutfitSummaryDto(productsImages)).ToList();

        var result = new PagedResult<OutfitSummaryDto>
        {
            Items = items,
            PageNumber = 1,
            PageSize = items.Count > 0 ? items.Count : 1,
            TotalCount = items.Count
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);

        return result;
    }
}
