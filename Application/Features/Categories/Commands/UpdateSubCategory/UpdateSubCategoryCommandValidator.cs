
namespace Application.Features.Categories.Commands.UpdateSubCategory;

public sealed class UpdateSubCategoryCommandValidator
    : AbstractValidator<UpdateSubCategoryCommand>
{
    public UpdateSubCategoryCommandValidator()
    {
        RuleFor(x => x.ParentCategoryId)
            .NotEmpty().WithMessage("Parent category ID is required."); 

        RuleFor(x => x.SubCategoryId)
            .NotEmpty().WithMessage("Sub-category ID is required.");

        RuleFor(x => x.NewName)
            .MinimumLength(1).WithMessage("Sub-category name must not be blank if provided.")
            .Must(n => !string.IsNullOrWhiteSpace(n))
            .WithMessage("Sub-category name must not be blank if provided.") 
            .MaximumLength(150).WithMessage("Sub-category name must not exceed 150 characters.")
            .When(x => x.NewName is not null);

        RuleFor(x => x.Status)
            .Must(s => s is null
                       || s == Domain.Entities.Retailer.SubCategory.SubCategoryStatus.Active
                       || s == Domain.Entities.Retailer.SubCategory.SubCategoryStatus.Inactive)
            .WithMessage("Status must be 'Active' or 'Inactive' if provided.")
            .When(x => x.Status is not null);
    }
}