using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Catalog.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Favorites.Queries.GetFavorites;

internal sealed class GetFavoritesQueryHandler : IRequestHandler<GetFavoritesQuery, PagedResult<ProductCardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetFavoritesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<ProductCardDto>> Handle(GetFavoritesQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can view favorites.");

        // 1. Fetch Paginated Favorite IDs
        var query = _context.CustomerFavorites
            .AsNoTracking()
            .Where(f => f.CustomerId == customerId)
            .OrderByDescending(f => f.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var favoritedProductIds = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(f => f.ProductId)
            .ToListAsync(cancellationToken);

        if (favoritedProductIds.Count == 0)
        {
            return new PagedResult<ProductCardDto>
            {
                Items = [],
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }

        // 2. Fetch Hydrated Product Data
        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Where(p => favoritedProductIds.Contains(p.Id) && p.Status == ProductStatus.Active)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var activeProductIds = products.Select(p => p.Id).ToList();

        // 3. Fetch Active Offers 
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeOffers = await _context.Offers
            .AsNoTracking()
            .Where(o => o.Status == Domain.Enums.Offer.OfferStatus.Active
                     && o.StartDate <= today
                     && (o.EndDate == null || o.EndDate >= today)
                     && o.OfferType == Domain.Enums.Offer.OfferType.Product
                     && o.ProductId.HasValue
                     && activeProductIds.Contains(o.ProductId.Value)) 
            .ToDictionaryAsync(o => o.ProductId!.Value, cancellationToken); 

        // 4. Map Results 
        var resultItems = new List<ProductCardDto>();
        foreach (var favProductId in favoritedProductIds)
        {
            var p = products.FirstOrDefault(x => x.Id == favProductId);
            if (p != null)
            {
                activeOffers.TryGetValue(p.Id, out var offer);
                resultItems.Add(p.ToProductCardDto(offer, true)); // isFavorite is always true
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
