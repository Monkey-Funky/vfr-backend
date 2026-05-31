using Application.Features.Categories.DTOs;
using Application.Features.Categories.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Queries.GetCategoryById;

/// <summary>
/// Returns a single category with its sub-category count for the authenticated retailer.
/// Cache-aside: TTL 30 minutes. Invalidated by category mutation commands.
/// </summary>
public sealed class GetCategoryByIdQueryHandler
    : IRequestHandler<GetCategoryByIdQuery, CategoryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetCategoryByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<CategoryDto> Handle(
        GetCategoryByIdQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey = $"category:{retailerId:N}:{query.CategoryId:N}";

        var cached = await _cacheService.GetAsync<CategoryDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        Category category = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == query.CategoryId && c.RetailerId == retailerId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Category), query.CategoryId);

        int subCategoryCount = await _context.SubCategories
            .AsNoTracking()
            .CountAsync(sc => sc.CategoryId == category.Id, cancellationToken);

        var dto = category.ToDto(subCategoryCount);

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(30), cancellationToken);

        return dto;
    }
}
