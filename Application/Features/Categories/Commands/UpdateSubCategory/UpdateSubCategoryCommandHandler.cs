
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Categories.Commands.UpdateSubCategory;

public sealed class UpdateSubCategoryCommandHandler
    : IRequestHandler<UpdateSubCategoryCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public UpdateSubCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<bool>> Handle(
        UpdateSubCategoryCommand command,
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

        // Name uniqueness within parent category (exclude self)
        if (command.NewName is not null)
        {
            string trimmedName = command.NewName.Trim().ToLower();

            bool nameConflict = await _unitOfWork.Repository<SubCategory>().AnyAsync(
                sc => sc.CategoryId == subCategory.CategoryId
                      && sc.Id != command.SubCategoryId
                      && sc.Name.ToLower() == trimmedName,
                cancellationToken);

            if (nameConflict)
                throw new ConflictException(nameof(SubCategory), "Name", command.NewName);
        }

        subCategory.Update(command.NewName, command.Status);

        await _unitOfWork.Repository<SubCategory>().UpdateAsync(subCategory, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveByPrefixAsync($"categories:{retailerId}:", cancellationToken);

        return Result<bool>.Success(true, "Sub-category updated successfully.");
    }
}