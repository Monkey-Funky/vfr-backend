using Domain.Entities.Retailer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Queries.GetSubCategories;


public sealed class GetSubCategoriesQueryHandler
    : IRequestHandler<GetSubCategoriesQuery, IReadOnlyList<SubCategoryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSubCategoriesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<SubCategoryDto>> Handle(
        GetSubCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

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

        return subCategories.Select(sc => sc.ToDto()).ToList();
    }
}