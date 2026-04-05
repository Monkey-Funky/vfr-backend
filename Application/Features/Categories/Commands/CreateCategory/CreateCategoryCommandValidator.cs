namespace Application.Features.Categories.Commands.CreateCategory;

public sealed class CreateCategoryCommandValidator
    : AbstractValidator<CreateCategoryCommand>
{
    private const long MaxCoverImageBytes = 1 * 1024 * 1024; // 1 MB

    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .Must(n => !string.IsNullOrWhiteSpace(n))
            .WithMessage("Category name must not be blank.") 
            .MaximumLength(150).WithMessage("Category name must not exceed 150 characters.");

        RuleFor(x => x.Description)
            .MinimumLength(1).WithMessage("Description must not be blank if provided.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.CoverImageStream)
            .NotNull().WithMessage("Cover image is required.");

        RuleFor(x => x.CoverImageStream)
            .Must(stream => stream.Length > 0)
            .WithMessage("Cover image file must not be empty.")
            .Must(stream => stream.Length <= MaxCoverImageBytes)
            .WithMessage("Cover image must not exceed 1 MB.")
            .When(x => x.CoverImageStream is not null);

        RuleFor(x => x.CoverImageFileName)
            .NotEmpty().WithMessage("Cover image file name is required.");

        RuleFor(x => x.CoverImageContentType)
            .NotEmpty().WithMessage("Cover image content type is required.")
            .Must(ct => ct is "image/jpeg" or "image/png" or "image/webp")
            .WithMessage("Cover image must be JPEG, PNG, or WebP.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => s == Domain.Entities.Retailer.Category.CategoryStatus.Active
                       || s == Domain.Entities.Retailer.Category.CategoryStatus.Inactive)
            .WithMessage("Status must be 'Active' or 'Inactive'.");
    }
}