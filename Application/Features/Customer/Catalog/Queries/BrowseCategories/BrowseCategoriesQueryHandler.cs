using Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Application.Features.Customer.Catalog.Queries.BrowseCategories;

internal sealed class BrowseCategoriesQueryHandler : IRequestHandler<BrowseCategoriesQuery, PagedResult<CategoryBrowseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedCache _cache;

    public BrowseCategoriesQueryHandler(IApplicationDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<CategoryBrowseDto>> Handle(BrowseCategoriesQuery request, CancellationToken cancellationToken)
    {
        string cacheKey = $"catalog:categories:retailer_{request.RetailerId?.ToString() ?? "all"}";
        
        var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedResult))
        {
            var dtos = JsonSerializer.Deserialize<CategoryBrowseDto[]>(cachedResult);
            if (dtos != null)
            {
                return new PagedResult<CategoryBrowseDto>
                {
                    Items = dtos,
                    TotalCount = dtos.Length,
                    PageNumber = 1,
                    PageSize = dtos.Length == 0 ? 1 : dtos.Length
                };
            }
        }

        var query = _context.Categories.AsNoTracking();

        if (request.RetailerId.HasValue)
        {
            query = query.Where(c => c.RetailerId == request.RetailerId.Value);
        }

        // Active and not deleted are handled by EF global query filters assuming Category has Status/IsDeleted
        // Wait, Category might not have Status.
        // Also we need to count products in each category.
        
        var categories = await query
            .Select(c => new CategoryBrowseDto(
                c.Id,
                c.Name,
                c.Description,
                _context.Products.Count(p => p.CategoryId == c.Id && p.Status == "Active" && !p.IsDeleted)
            ))
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        // Cache for 15 minutes
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(15)
        };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(categories), cacheOptions, cancellationToken);

        return new PagedResult<CategoryBrowseDto>
        {
            Items = categories,
            TotalCount = categories.Count,
            PageNumber = 1,
            PageSize = categories.Count == 0 ? 1 : categories.Count
        };
    }
}
