using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Application.Features.Categories.Commands.CreateSubCategory;

public sealed class CreateSubCategoryCommandHandler
    : IRequestHandler<CreateSubCategoryCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public CreateSubCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<Guid>> Handle(
        CreateSubCategoryCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // Step 1: Verify parent category belongs to this retailer
        Category parentCategory = await _unitOfWork.Repository<Category>().FirstOrDefaultAsync(
            c => c.Id == command.ParentCategoryId && c.RetailerId == retailerId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(Category), command.ParentCategoryId);

        // Step 2: Depth limit enforcement — parent must be a Category, not a SubCategory.
        // Since we fetched it from the Category repository and it exists, it is definitionally
        // a top-level Category. However, as a belt-and-suspenders check we also ensure the
        // parentCategoryId does NOT exist as a SubCategory Id.
        bool parentIsSubCategory = await _unitOfWork.Repository<SubCategory>().AnyAsync(
            sc => sc.Id == command.ParentCategoryId,
            cancellationToken);

        if (parentIsSubCategory)
            throw new BusinessRuleException(
                "SUBCATEGORY_DEPTH_EXCEEDED",
                "Sub-categories cannot have their own sub-categories. Maximum depth is 1.");

        // Step 3: Name uniqueness within the parent category
        bool nameExists = await _unitOfWork.Repository<SubCategory>().AnyAsync(
            sc => sc.CategoryId == command.ParentCategoryId
                  && sc.Name.ToLower() == command.Name.Trim().ToLower(),
            cancellationToken);

        if (nameExists)
            throw new ConflictException(nameof(SubCategory), "Name", command.Name);

        SubCategory subCategory = SubCategory.Create(
            categoryId: parentCategory.Id,
            retailerId: retailerId,
            name: command.Name,
            status: command.Status);

        try
        {
            await _unitOfWork.Repository<SubCategory>().AddAsync(subCategory, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // BUG-005 FIX: Concurrent insert with the same name raced past the AnyAsync check.
            // Convert the DB constraint violation to a clean 409 Conflict instead of a 500.
            throw new ConflictException(nameof(SubCategory), "Name", command.Name);
        }

        await _cacheService.RemoveByPrefixAsync($"categories:{retailerId}:", cancellationToken);

        return Result<Guid>.Success(subCategory.Id, "Sub-category created successfully.");
    }
}