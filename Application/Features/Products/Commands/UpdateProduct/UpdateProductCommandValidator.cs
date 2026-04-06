using Domain.Enums.Product;

namespace Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(c => c.ProductId)
            .NotEmpty().WithMessage("ProductId is required.");

        RuleFor(c => c.NewName)
            .NotEmpty().WithMessage("Product name cannot be empty when provided.")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters.")
            .When(c => c.NewName is not null);

        RuleFor(c => c.NewDescription)
            .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters.")
            .When(c => c.ShouldUpdateDescription && c.NewDescription is not null);

        RuleFor(c => c.NewPrice)
            .GreaterThan(0).WithMessage("Price must be greater than zero.")
            .When(c => c.ShouldUpdatePrice && c.NewPrice.HasValue);

        RuleFor(c => c.NewBarcode)
            .MaximumLength(100).WithMessage("Barcode cannot exceed 100 characters.")
            .Matches(@"^[a-zA-Z0-9\-_]+$").WithMessage("Barcode must be alphanumeric.")
            .When(c => c.ShouldUpdateBarcode && !string.IsNullOrWhiteSpace(c.NewBarcode));

        RuleFor(c => c.NewStatus)
            .Must(s => s == null || ProductStatus.IsValid(s))
            .WithMessage($"Status must be one of: {string.Join(", ", ProductStatus.All)}.")
            .When(c => c.NewStatus is not null);
    }
}
