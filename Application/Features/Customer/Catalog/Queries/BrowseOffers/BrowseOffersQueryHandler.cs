using Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Catalog.Queries.BrowseOffers;

internal sealed class BrowseOffersQueryHandler : IRequestHandler<BrowseOffersQuery, PagedResult<OfferBrowseDto>>
{
    private readonly IApplicationDbContext _context;

    public BrowseOffersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<OfferBrowseDto>> Handle(BrowseOffersQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.Offers.AsNoTracking()
            .Where(o => o.Status == "Active" && o.StartDate <= today && (o.EndDate == null || o.EndDate >= today));

        if (request.RetailerId.HasValue)
        {
            query = query.Where(o => o.RetailerId == request.RetailerId.Value);
        }

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

        return new PagedResult<OfferBrowseDto>
        {
            Items = offers,
            TotalCount = offers.Count,
            PageNumber = 1,
            PageSize = offers.Count == 0 ? 1 : offers.Count
        };
    }
}
