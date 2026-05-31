using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Categories.Commands.DeleteSubCategory;

/// <summary>
/// Soft-deletes a sub-category owned by the current retailer.
/// Cascade: nulls out SubCategoryId on all affected products within the same transaction.
/// Cache: invalidates both the category list prefix and the specific subcategory key.
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
            // Step 1: Null out products.SubCategoryId within the same transaction.
            var affectedProducts = await _context.Products
                .Where(p => p.SubCategoryId == command.SubCategoryId)
                .ToListAsync(ct);

            foreach (var product in affectedProducts)
                product.UpdateCategory(product.CategoryId, null);

            // Step 2: Soft-delete the sub-category itself.
            subCategory.MarkAsDeleted();
            await _unitOfWork.Repository<SubCategory>().UpdateAsync(subCategory, ct);
            await _unitOfWork.SaveChangesAsync(ct);

        }, cancellationToken);

        // Invalidate: category list prefix + the specific subcategory lookup key.
        // FIX: use command.ParentCategoryId (the correct property) not command.CategoryId (doesn't exist).
        await Task.WhenAll(
            _cacheService.RemoveByPrefixAsync($"categories:{retailerId}:", cancellationToken),
            _cacheService.RemoveAsync(CacheKeys.SubCategories(retailerId, command.ParentCategoryId), cancellationToken)
        );

        return Result<bool>.Success(true, "Sub-category deleted successfully.");
    }
}
