using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Catalog.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Wardrobe.Queries.GetCollectionItems;

internal sealed class GetCollectionItemsQueryHandler : IRequestHandler<GetCollectionItemsQuery, PagedResult<ProductCardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetCollectionItemsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<ProductCardDto>> Handle(GetCollectionItemsQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId;

        // 1. Secure IDOR Check (Combined)
        var collectionExists = await _context.WardrobeCollections
            .AnyAsync(c => c.Id == request.CollectionId && c.CustomerId == customerId, cancellationToken);

        if (!collectionExists)
            throw new NotFoundException("WardrobeCollection", request.CollectionId);

        // 2. Fetch the paginated Product IDs for this collection
        var query = _context.WardrobeCollectionItems
            .AsNoTracking()
            .Where(i => i.CollectionId == request.CollectionId)
            .Join(_context.CustomerFavorites.AsNoTracking(),
                i => i.FavoriteId,
                f => f.Id,
                (i, f) => new { i.CreatedAt, f.ProductId })
            .OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var productIds = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => x.ProductId)
            .ToListAsync(cancellationToken);

        if (productIds.Count == 0)
        {
            return new PagedResult<ProductCardDto>
            {
                Items = [],
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }

        // 3. Fetch Product Details
        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Where(p => productIds.Contains(p.Id) && p.Status == ProductStatus.Active)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        // 4. Fetch Active Offers (CRITICAL FIX: Filtered by productIds)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeOffers = await _context.Offers
            .AsNoTracking()
            .Where(o => o.Status == Domain.Enums.Offer.OfferStatus.Active
                     && o.StartDate <= today
                     && (o.EndDate == null || o.EndDate >= today)
                     && o.OfferType == Domain.Enums.Offer.OfferType.Product
                     && o.ProductId.HasValue
                     && productIds.Contains(o.ProductId.Value)) // The database does the filtering now!
            .ToDictionaryAsync(o => o.ProductId!.Value, cancellationToken); // Instantly mapped to a dictionary!

        // 5. Hydrate Results
        var resultItems = new List<ProductCardDto>();
        foreach (var pId in productIds) // Iterate by original sorted order
        {
            var p = products.FirstOrDefault(x => x.Id == pId);
            if (p != null)
            {
                activeOffers.TryGetValue(p.Id, out var offer);
                resultItems.Add(p.ToProductCardDto(offer, true)); // IsFavorite is true
            }
        }

        return new PagedResult<ProductCardDto>
        {
            Items = resultItems,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}