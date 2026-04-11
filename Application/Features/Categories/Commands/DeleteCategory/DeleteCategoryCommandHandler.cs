using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Offer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Categories.Commands.DeleteCategory;

/// <summary>
/// Handles <see cref="DeleteCategoryCommand"/>.
///
/// DESIGN NOTE — Dual injection:
///   This handler injects both <see cref="IUnitOfWork"/> (for transaction management
///   and category / sub-category operations) and <see cref="IApplicationDbContext"/>
///   (for efficient bulk <c>ExecuteUpdateAsync</c> on Offers and Products).
///   Both services resolve to the same underlying <c>ApplicationDbContext</c> instance
///   per request scope, so they share the same connection and participate in the same
///   PostgreSQL transaction opened by <c>ExecuteInTransactionAsync</c>.
///
///   This exception to the "command handlers inject only IUnitOfWork" convention is
///   intentional and scoped to cascade operations that must bulk-update unrelated
///   aggregate roots (Offers, Products) without loading thousands of entities.
/// </summary>
public sealed class DeleteCategoryCommandHandler
    : IRequestHandler<DeleteCategoryCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICacheService _cacheService;

    public DeleteCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _cacheService = cacheService;
    }

    public async Task<Result<bool>> Handle(
        DeleteCategoryCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // IDOR guard: load category and verify ownership
        Category category = await _unitOfWork.Repository<Category>().FirstOrDefaultAsync(
            c => c.Id == command.CategoryId && c.RetailerId == retailerId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(Category), command.CategoryId);

        string coverImageUrlToDelete = category.CoverImageUrl;
        DateTime now = DateTime.UtcNow;

        // All cascade operations execute inside a single PostgreSQL transaction
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Step 1: Set offers with this category → Inactive (+ stamp UpdatedAt)
            // BUG-001 FIX: UpdatedAt set explicitly — ExecuteUpdateAsync bypasses SaveChangesAsync.
            await _context.Offers
                .Where(o => o.CategoryId == command.CategoryId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(o => o.Status, OfferStatus.Inactive)
                        .SetProperty(o => o.UpdatedAt, now),
                    ct);

            // Step 2: Null out products.CategoryId for products in this category (+ stamp UpdatedAt)
            await _context.Products
                .Where(p => p.CategoryId == command.CategoryId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(p => p.CategoryId, (Guid?)null)
                        .SetProperty(p => p.UpdatedAt, now),
                    ct);

            // Step 3: Soft-delete all SubCategories belonging to this category (+ stamp UpdatedAt)
            await _context.SubCategories
                .Where(sc => sc.CategoryId == command.CategoryId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(sc => sc.IsDeleted, true)
                        .SetProperty(sc => sc.UpdatedAt, now),
                    ct);

            // Step 4: Soft-delete the category itself
            // SaveChangesAsync stamps UpdatedAt via the ApplicationDbContext override.
            category.MarkAsDeleted();
            await _unitOfWork.Repository<Category>().UpdateAsync(category, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        // Delete the cover image from S3 after the DB transaction commits successfully
        await _fileStorageService.DeleteAsync(coverImageUrlToDelete, cancellationToken);

        // Invalidate all category cache entries for this retailer
        await _cacheService.RemoveByPrefixAsync($"categories:{retailerId}:", cancellationToken);

        return Result<bool>.Success(true, "Category deleted successfully.");
    }
}