using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
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

        // Step 1: Verify parent category belongs to this retailer.
        // FirstOrDefaultAsync matches the IRepository contract used by all tests and callers.
        Category parentCategory = await _unitOfWork.Repository<Category>().FirstOrDefaultAsync(
            c => c.Id == command.ParentCategoryId && c.RetailerId == retailerId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(Category), command.ParentCategoryId);

        // Step 2: Depth limit — parent must be a Category (not itself a SubCategory)
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
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new ConflictException(nameof(SubCategory), "Name", command.Name);
        }

        await _cacheService.RemoveByPrefixAsync($"categories:{retailerId}:", cancellationToken);

        return Result<Guid>.Success(subCategory.Id, "Sub-category created successfully.");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505") == true
        || ex.InnerException?.Message.Contains("unique constraint") == true
        || ex.InnerException?.Message.Contains("unique_violation") == true;
}