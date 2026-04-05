using Domain.Entities.Retailer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Queries.GetCategoryById;


public sealed class GetCategoryByIdQueryHandler
    : IRequestHandler<GetCategoryByIdQuery, CategoryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetCategoryByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<CategoryDto> Handle(
        GetCategoryByIdQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        Category category = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == query.CategoryId && c.RetailerId == retailerId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Category), query.CategoryId);

        int subCategoryCount = await _context.SubCategories
            .AsNoTracking()
            .CountAsync(sc => sc.CategoryId == category.Id, cancellationToken);

        return category.ToDto(subCategoryCount);
    }
}