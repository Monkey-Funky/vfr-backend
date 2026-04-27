using Application.Features.Customer.Catalog.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Application.Features.Customer.Catalog.Mappings;

namespace Application.Features.Customer.Catalog.Queries.GetSimilarProducts;

internal sealed class GetSimilarProductsQueryHandler : IRequestHandler<GetSimilarProductsQuery, List<ProductCardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSimilarProductsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ProductCardDto>> Handle(GetSimilarProductsQuery request, CancellationToken cancellationToken)
    {
        var sourceProduct = await _context.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.Status == ProductStatus.Active, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        var query = _context.Products.AsNoTracking()
            .Where(p => p.Id != sourceProduct.Id && p.Status == ProductStatus.Active);

        // Filter by same category OR same brand
        query = query.Where(p => 
            (sourceProduct.CategoryId.HasValue && p.CategoryId == sourceProduct.CategoryId.Value) ||
            (!string.IsNullOrWhiteSpace(sourceProduct.Brand) && p.Brand == sourceProduct.Brand)
        );

        // Order by views_count DESC
        query = query.OrderByDescending(p => p.ViewsCount);

        var products = await query
            .Include(p => p.Images)
            .Take(request.Limit)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var productIds = products.Select(p => p.Id).ToList();
        var categoryIds = products.Where(p => p.CategoryId.HasValue).Select(p => p.CategoryId!.Value).Distinct().ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var activeOffers = await _context.Offers.AsNoTracking()
            .Where(o => o.Status == "Active" && o.StartDate <= today && (o.EndDate == null || o.EndDate >= today))
            .Where(o => (o.ProductId.HasValue && productIds.Contains(o.ProductId.Value)) || 
                        (o.CategoryId.HasValue && categoryIds.Contains(o.CategoryId.Value)))
            .ToListAsync(cancellationToken);

        HashSet<Guid> favoriteProductIds = new();
        if (_currentUserService.IsAuthenticated && _currentUserService.CustomerId != Guid.Empty)
        {
            var customerId = _currentUserService.CustomerId;
            var favorites = await _context.CustomerFavorites.AsNoTracking()
                .Where(f => f.CustomerId == customerId && productIds.Contains(f.ProductId))
                .Select(f => f.ProductId)
                .ToListAsync(cancellationToken);
            
            favoriteProductIds = [.. favorites];
        }

        var dtos = products.Select(p => 
        {
            var offer = activeOffers.FirstOrDefault(o => o.ProductId == p.Id) ?? 
                        activeOffers.FirstOrDefault(o => o.CategoryId == p.CategoryId);

            return p.ToProductCardDto(offer, favoriteProductIds.Contains(p.Id));
        }).ToList();

        return dtos;
    }
}
