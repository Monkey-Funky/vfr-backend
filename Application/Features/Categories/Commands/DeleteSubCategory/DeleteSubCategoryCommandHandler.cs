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
            // Step 1: Null out products.SubCategoryId (+ let SaveChanges stamp UpdatedAt).
            // Load into the change tracker and mutate via the domain method so EF Core
            // detects the change. This replaces the previous ExecuteUpdateAsync call,
            // which is not supported by the in-memory / mocked DbSet in unit tests.
            var affectedProducts = await _context.Products
                .Where(p => p.SubCategoryId == command.SubCategoryId)
                .ToListAsync(ct);

            foreach (var product in affectedProducts)
                product.UpdateCategory(product.CategoryId, null); // clear SubCategoryId only

            // Step 2: Soft-delete the sub-category itself
            subCategory.MarkAsDeleted();
            await _unitOfWork.Repository<SubCategory>().UpdateAsync(subCategory, ct);
            await _unitOfWork.SaveChangesAsync(ct);

        }, cancellationToken);

        await _cacheService.RemoveByPrefixAsync($"categories:{retailerId}:", cancellationToken);

        return Result<bool>.Success(true, "Sub-category deleted successfully.");
    }
}