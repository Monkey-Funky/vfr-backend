using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Commands.DeleteSubCategory;

/// <summary>
/// DESIGN NOTE: Injects both <see cref="IUnitOfWork"/> and <see cref="IApplicationDbContext"/>
/// for the same reason as <c>DeleteCategoryCommandHandler</c> — see that handler's doc comment.
/// Both resolve to the same scoped <c>ApplicationDbContext</c> instance and share the same
/// PostgreSQL transaction opened by <c>ExecuteInTransactionAsync</c>.
/// </summary>
public sealed class DeleteSubCategoryCommandHandler
    : IRequestHandler<DeleteSubCategoryCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public DeleteSubCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<bool>> Handle(
        DeleteSubCategoryCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // IDOR guard: verify the sub-category belongs to this retailer AND to the specified parent.
        SubCategory subCategory = await _unitOfWork.Repository<SubCategory>().FirstOrDefaultAsync(
            sc => sc.Id == command.SubCategoryId
                  && sc.RetailerId == retailerId
                  && sc.CategoryId == command.ParentCategoryId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(SubCategory), command.SubCategoryId);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Step 1: Null out products.SubCategoryId for products in this sub-category.
            // stamped explicitly in the setter chain — the DbContext override never fires here.
            await _context.Products
                .Where(p => p.SubCategoryId == command.SubCategoryId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(p => p.SubCategoryId, (Guid?)null)
                        .SetProperty(p => p.UpdatedAt, DateTime.UtcNow),
                    ct);

            // Step 2: Soft-delete the sub-category itself.
            // SaveChangesAsync stamps UpdatedAt via the ApplicationDbContext override.
            subCategory.MarkAsDeleted();
            await _unitOfWork.Repository<SubCategory>().UpdateAsync(subCategory, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        await _cacheService.RemoveByPrefixAsync($"categories:{retailerId}:", cancellationToken);

        return Result<bool>.Success(true, "Sub-category deleted successfully.");
    }
}