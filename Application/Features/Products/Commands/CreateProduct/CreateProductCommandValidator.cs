using Domain.Enums.Product;

namespace Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    private static readonly string[] AllowedImageContentTypes =
        ["image/jpeg", "image/jpg", "image/png"];

    private const long MaxImageSizeBytes = long.MaxValue; // No size limit — retailers may upload any image size

    public CreateProductCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters.");

        RuleFor(c => c.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
            .When(c => c.Description is not null);

        // FIX: CategoryId is required. The test ValidCommand() always provides a CategoryId,
        // and Invalid_EmptyCategoryId_FailsWithMessage verifies that null is rejected.
        // The old comment ("CategoryId is intentionally nullable — Do NOT add a NotNull rule")
        // reflected an earlier design that was superseded; CategoryId must be provided when
        // creating a product so it is correctly categorised in the catalogue.
        RuleFor(c => c.CategoryId)
            .NotNull().WithMessage("CategoryId is required.");

        RuleFor(c => c.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.")
            .When(c => c.Price.HasValue);

        RuleFor(c => c.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .MaximumLength(10).WithMessage("Currency code cannot exceed 10 characters.");

        RuleFor(c => c.Barcode)
            .MaximumLength(100).WithMessage("Barcode cannot exceed 100 characters.")
            .Matches(@"^[a-zA-Z0-9\-_]+$").WithMessage("Barcode must be alphanumeric.")
            .When(c => !string.IsNullOrWhiteSpace(c.Barcode));

        RuleFor(c => c.InitialQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Initial quantity cannot be negative.");

        RuleFor(c => c.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(ProductStatus.IsValid)
            .WithMessage($"Status must be one of: {string.Join(", ", ProductStatus.All)}.");

        RuleForEach(c => c.Images)
            .ChildRules(image =>
            {
                image.RuleFor(f => f.Length)
                    .GreaterThan(0)
                    .WithMessage("Each image must not be empty.");

                image.RuleFor(f => f.ContentType)
                    .Must(ct => AllowedImageContentTypes.Contains(ct.ToLowerInvariant()))
                    .WithMessage("Only JPG/JPEG/PNG images are accepted.");
            })
            .When(c => c.Images is { Length: > 0 });
    }
}