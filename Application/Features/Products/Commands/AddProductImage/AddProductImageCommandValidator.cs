
namespace Application.Features.Products.Commands.AddProductImage;

public sealed class AddProductImageCommandValidator
    : AbstractValidator<AddProductImageCommand>
{
    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/jpg", "image/png"];

    private const long MaxSizeBytes = 1 * 1024 * 1024; // 1 MB

    public AddProductImageCommandValidator()
    {
        RuleFor(c => c.ProductId)
            .NotEmpty().WithMessage("ProductId is required.");

        RuleFor(c => c.ImageFile)
            .NotNull().WithMessage("Image file is required.");

        // FileUploadDto.Length — same property name as IFormFile.Length
        RuleFor(c => c.ImageFile.Length)
            .LessThanOrEqualTo(MaxSizeBytes)
            .WithMessage("Image must not exceed 1 MB.")
            .When(c => c.ImageFile is not null);

        // FileUploadDto.ContentType — same property name as IFormFile.ContentType
        RuleFor(c => c.ImageFile.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct.ToLowerInvariant()))
            .WithMessage("Only JPG/JPEG/PNG images are accepted.")
            .When(c => c.ImageFile is not null);

        RuleFor(c => c.DisplayOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Display order cannot be negative.");
    }
}