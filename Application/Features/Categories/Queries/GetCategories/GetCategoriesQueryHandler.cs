using Application.Features.Categories.DTOs;
using Application.Features.Categories.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Queries.GetCategories;

/// <summary>
/// Query handler for <see cref="GetCategoriesQuery"/>.
///
/// Cache strategy: cache-aside, TTL 30 minutes.
/// Cache key: "categories:{retailerId}:p{page}s{size}:status{status}"
/// Invalidated by every write command in the Categories feature.
/// </summary>
public sealed class GetCategoriesQueryHandler
    : IRequestHandler<GetCategoriesQuery, PagedResult<CategoryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetCategoriesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<CategoryDto>> Handle(
        GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"categories:{retailerId}:" +
            $"p{query.PageNumber}s{query.PageSize}" +
            $":status{query.Status ?? "null"}";

        PagedResult<CategoryDto>? cached =
            await _cacheService.GetAsync<PagedResult<CategoryDto>>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        IQueryable<Category> queryable = _context.Categories
            .AsNoTracking()
            .Where(c => c.RetailerId == retailerId);

        if (query.Status is not null)
            queryable = queryable.Where(c => c.Status == query.Status);

        int totalCount = await queryable.CountAsync(cancellationToken);

        List<Category> categories = await queryable
            .OrderBy(c => c.Name)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            // BUG-008 FIX: Removed .AsSplitQuery() — no Include() is present here.
            .ToListAsync(cancellationToken);

        // Fetch sub-category counts in a single IN query to avoid N+1
        List<Guid> categoryIds = categories.Select(c => c.Id).ToList();

        Dictionary<Guid, int> subCountByCategory = await _context.SubCategories
            .AsNoTracking()
            .Where(sc => categoryIds.Contains(sc.CategoryId))
            .GroupBy(sc => sc.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(
                x => x.CategoryId,
                x => x.Count,
                cancellationToken);

        List<CategoryDto> dtos = categories
            .Select(c => c.ToDto(subCountByCategory.GetValueOrDefault(c.Id, 0)))
            .ToList();

        PagedResult<CategoryDto> result = new()
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
        };

        await _cacheService.SetAsync(
            cacheKey,
            result,
            TimeSpan.FromMinutes(30),
            cancellationToken);

        return result;
    }
}