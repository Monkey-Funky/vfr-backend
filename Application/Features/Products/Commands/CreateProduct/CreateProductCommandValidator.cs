namespace Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    private static readonly string[] AllowedImageContentTypes =
        ["image/jpeg", "image/jpg", "image/png"];

    private const long MaxImageSizeBytes = 1 * 1024 * 1024; // 1 MB

    public CreateProductCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters.");

        RuleFor(c => c.Description)
            .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters.")
            .When(c => c.Description is not null);

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

        // FileUploadDto.Length and .ContentType — same property names as IFormFile
        RuleForEach(c => c.Images)
            .ChildRules(image =>
            {
                image.RuleFor(f => f.Length)
                    .LessThanOrEqualTo(MaxImageSizeBytes)
                    .WithMessage("Each image must not exceed 1 MB.");

                image.RuleFor(f => f.ContentType)
                    .Must(ct => AllowedImageContentTypes.Contains(ct.ToLowerInvariant()))
                    .WithMessage("Only JPG/JPEG/PNG images are accepted.");
            })
            .When(c => c.Images is { Length: > 0 });
    }
}