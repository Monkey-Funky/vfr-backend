using Application.Features.Customer.Catalog.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Application.Features.Customer.Catalog.Mappings;
using NpgsqlTypes;

namespace Application.Features.Customer.Catalog.Queries.BrowseProducts;

internal sealed class BrowseProductsQueryHandler : IRequestHandler<BrowseProductsQuery, PagedResult<ProductCardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public BrowseProductsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<ProductCardDto>> Handle(BrowseProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products.AsNoTracking();

        // 1. Base Filters (Retailer global filters like IsDeleted and Active are assumed applied or handled via EF)
        // Ensure we only see Active products just in case
        query = query.Where(p => p.Status == ProductStatus.Active);

        if (request.RetailerId.HasValue)
            query = query.Where(p => p.RetailerId == request.RetailerId.Value);

        if (request.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);

        if (request.SubCategoryId.HasValue)
            query = query.Where(p => p.SubCategoryId == request.SubCategoryId.Value);

        if (request.MinPrice.HasValue)
            query = query.Where(p => p.Price >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= request.MaxPrice.Value);

        //2.Full - Text Search(plainto_tsquery)
        if (!string.IsNullOrWhiteSpace(request.SearchTerm) && request.SearchTerm.Length >= 2)
        {
            query = query.Where(p => EF.Property<NpgsqlTsVector>(p, "search_vector")
                .Matches(EF.Functions.PlainToTsQuery("english", request.SearchTerm)));
        }

        // 3. Array Filters (Multi-select)
        if (request.Categories != null && request.Categories.Length > 0)
        {
            var categoryIdsToFilter = await _context.Categories.AsNoTracking()
                .Where(c => request.Categories.Contains(c.Name))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
            
            if (categoryIdsToFilter.Count > 0)
            {
                query = query.Where(p => p.CategoryId.HasValue && categoryIdsToFilter.Contains(p.CategoryId.Value));
            }
            else
            {
                // No matching categories found, so filter out everything
                query = query.Where(p => false);
            }
        }

        if (request.Colors != null && request.Colors.Length > 0)
        {
            query = query.Where(p => p.AvailableColors != null && p.AvailableColors.Any(c => request.Colors.Contains(c)));
        }

        if (request.Sizes != null && request.Sizes.Length > 0)
        {
            query = query.Where(p => p.AvailableSizes != null && p.AvailableSizes.Any(s => request.Sizes.Contains(s)));
        }

        if (request.FabricMaterials != null && request.FabricMaterials.Length > 0)
        {
            query = query.Where(p => p.Material != null && request.FabricMaterials.Contains(p.Material));
        }

        if (request.FabricPatterns != null && request.FabricPatterns.Length > 0)
        {
            query = query.Where(p => p.Pattern != null && request.FabricPatterns.Contains(p.Pattern));
        }

        if (request.Brands != null && request.Brands.Length > 0)
        {
            query = query.Where(p => p.Brand != null && request.Brands.Contains(p.Brand));
        }

        // Note: BodyShapes not currently mapped to Product directly, ignoring or needs joining later
        // if (request.BodyShapes != null && request.BodyShapes.Length > 0) { ... }

        // 4. Sorting
        query = request.SortBy?.ToLowerInvariant() switch
        {
            "price" => request.SortOrder?.ToLowerInvariant() == "desc" ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            "name" => request.SortOrder?.ToLowerInvariant() == "desc" ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "newest" => request.SortOrder?.ToLowerInvariant() == "desc" ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            "mostviewed" => request.SortOrder?.ToLowerInvariant() == "desc" ? query.OrderByDescending(p => p.ViewsCount) : query.OrderBy(p => p.ViewsCount),
            _ => query.OrderByDescending(p => p.CreatedAt) // Default Newest
        };

        // 5. Pagination & Projection
        int totalCount = await query.CountAsync(cancellationToken);
        
        var products = await query
            .Include(p => p.Images)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        // Fetch active offers for these products
        var productIds = products.Select(p => p.Id).ToList();
        var categoryIds = products.Where(p => p.CategoryId.HasValue).Select(p => p.CategoryId!.Value).Distinct().ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var activeOffers = await _context.Offers.AsNoTracking()
            .Where(o => o.Status == "Active" && o.StartDate <= today && (o.EndDate == null || o.EndDate >= today))
            .Where(o => (o.ProductId.HasValue && productIds.Contains(o.ProductId.Value)) || 
                        (o.CategoryId.HasValue && categoryIds.Contains(o.CategoryId.Value)))
            .ToListAsync(cancellationToken);

        // Compute IsFavorite
        HashSet<Guid> favoriteProductIds = new();
        if (_currentUserService.IsAuthenticated && _currentUserService.CustomerId.HasValue)
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
            decimal? discountedPrice = null;
            var offer = activeOffers.FirstOrDefault(o => o.ProductId == p.Id) ?? 
                        activeOffers.FirstOrDefault(o => o.CategoryId == p.CategoryId);

            return p.ToProductCardDto(offer, favoriteProductIds.Contains(p.Id));
        }).ToList();

        return new PagedResult<ProductCardDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
