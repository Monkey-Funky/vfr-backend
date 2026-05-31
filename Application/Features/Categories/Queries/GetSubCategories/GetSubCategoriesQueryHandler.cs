using Application.Features.Categories.DTOs;
using Application.Features.Categories.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Categories.Queries.GetSubCategories;

/// <summary>
/// Returns all sub-categories for a parent category.
/// Cache-aside: TTL 30 minutes. Invalidated by CreateSubCategory, UpdateSubCategory,
/// DeleteSubCategory command handlers.
/// </summary>
public sealed class GetSubCategoriesQueryHandler
    : IRequestHandler<GetSubCategoriesQuery, IReadOnlyList<SubCategoryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetSubCategoriesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyList<SubCategoryDto>> Handle(
        GetSubCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey = CacheKeys.SubCategories(retailerId, query.ParentCategoryId);

        var cached = await _cacheService.GetAsync<List<SubCategoryDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        // Validate the parent category exists and belongs to this retailer (→ 404 on miss)
        bool parentExists = await _context.Categories
            .AsNoTracking()
            .AnyAsync(
                c => c.Id == query.ParentCategoryId && c.RetailerId == retailerId,
                cancellationToken);

        if (!parentExists)
            throw new NotFoundException(nameof(Category), query.ParentCategoryId);

        List<SubCategory> subCategories = await _context.SubCategories
            .AsNoTracking()
            .Where(sc => sc.CategoryId == query.ParentCategoryId)
            .OrderBy(sc => sc.Name)
            .ToListAsync(cancellationToken);

        var result = subCategories.Select(sc => sc.ToDto()).ToList();

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30), cancellationToken);

        return result;
    }
}
