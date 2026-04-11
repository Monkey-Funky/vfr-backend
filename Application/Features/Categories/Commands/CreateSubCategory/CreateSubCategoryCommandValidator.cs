
namespace Application.Features.Categories.Commands.CreateSubCategory;

public sealed class CreateSubCategoryCommandValidator
    : AbstractValidator<CreateSubCategoryCommand>
{
    public CreateSubCategoryCommandValidator()
    {
        RuleFor(x => x.ParentCategoryId)
            .NotEmpty().WithMessage("Parent category ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Sub-category name is required.")
            .Must(n => !string.IsNullOrWhiteSpace(n))
            .WithMessage("Sub-category name must not be blank.") 
            .MaximumLength(150).WithMessage("Sub-category name must not exceed 150 characters.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => s == Domain.Entities.Retailer.SubCategory.SubCategoryStatus.Active
                       || s == Domain.Entities.Retailer.SubCategory.SubCategoryStatus.Inactive)
            .WithMessage("Status must be 'Active' or 'Inactive'.");
    }
}