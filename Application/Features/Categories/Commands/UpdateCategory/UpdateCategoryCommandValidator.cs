namespace Application.Features.Categories.Commands.UpdateCategory;

public sealed class UpdateCategoryCommandValidator
    : AbstractValidator<UpdateCategoryCommand>
{
    private const long MaxCoverImageBytes = 1 * 1024 * 1024; // 1 MB

    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category ID is required.");

        RuleFor(x => x.NewName)
            .MinimumLength(1).WithMessage("Category name must not be blank if provided.")
            .Must(n => !string.IsNullOrWhiteSpace(n))
            .WithMessage("Category name must not be blank if provided.") // BUG-007 FIX
            .MaximumLength(150).WithMessage("Category name must not exceed 150 characters.")
            .When(x => x.NewName is not null);

        RuleFor(x => x.NewDescription)
            .MinimumLength(1).WithMessage("Description must not be blank if provided.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.")
            .When(x => x.NewDescription is not null);

        RuleFor(x => x.NewCoverImageStream)
            .Must(stream => stream is not null && stream.Length > 0)
            .WithMessage("Cover image file must not be empty if provided.")
            .Must(stream => stream is null || stream.Length <= MaxCoverImageBytes)
            .WithMessage("Cover image must not exceed 1 MB.")
            .When(x => x.NewCoverImageStream is not null);

        RuleFor(x => x.NewCoverImageContentType)
            .Must(ct => ct is null || ct is "image/jpeg" or "image/png" or "image/webp")
            .WithMessage("Cover image must be JPEG, PNG, or WebP.")
            .When(x => x.NewCoverImageContentType is not null);

        RuleFor(x => x.Status)
            .Must(s => s is null
                       || s == Domain.Entities.Retailer.Category.CategoryStatus.Active
                       || s == Domain.Entities.Retailer.Category.CategoryStatus.Inactive)
            .WithMessage("Status must be 'Active' or 'Inactive' if provided.")
            .When(x => x.Status is not null);
    }
}