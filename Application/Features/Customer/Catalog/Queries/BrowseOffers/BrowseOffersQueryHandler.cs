using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Catalog.Queries.BrowseOffers;

/// <summary>
/// Returns active offers for the customer catalog.
/// Cache-aside: TTL 5 minutes. Invalidated by CreateOffer, UpdateOffer, DeleteOffer.
/// </summary>
internal sealed class BrowseOffersQueryHandler : IRequestHandler<BrowseOffersQuery, PagedResult<OfferBrowseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public BrowseOffersQueryHandler(IApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<OfferBrowseDto>> Handle(BrowseOffersQuery request, CancellationToken cancellationToken)
    {
        string cacheKey = $"catalog:offers:{request.RetailerId?.ToString() ?? "all"}";

        var cached = await _cacheService.GetAsync<PagedResult<OfferBrowseDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.Offers.AsNoTracking()
            .Where(o => o.Status == "Active" && o.StartDate <= today && (o.EndDate == null || o.EndDate >= today));

        if (request.RetailerId.HasValue)
            query = query.Where(o => o.RetailerId == request.RetailerId.Value);

        var offers = await query
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OfferBrowseDto(
                o.Id,
                o.Title,
                o.Description,
                o.OfferType,
                o.ProductId,
                o.CategoryId,
                o.DiscountType,
                o.DiscountValue,
                o.CoverImageUrl,
                o.StartDate,
                o.EndDate
            ))
            .ToListAsync(cancellationToken);

        var result = new PagedResult<OfferBrowseDto>
        {
            Items = offers,
            TotalCount = offers.Count,
            PageNumber = 1,
            PageSize = offers.Count == 0 ? 1 : offers.Count
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);

        return result;
    }
}
