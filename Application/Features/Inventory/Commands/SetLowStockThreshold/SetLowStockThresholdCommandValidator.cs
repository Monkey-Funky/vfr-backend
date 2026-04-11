namespace Application.Features.Inventory.Commands.SetLowStockThreshold;

public sealed class SetLowStockThresholdCommandValidator
    : AbstractValidator<SetLowStockThresholdCommand>
{
    public SetLowStockThresholdCommandValidator()
    {
        RuleFor(c => c.InventoryRecordId)
            .NotEmpty()
            .WithMessage("InventoryRecordId is required.");

        RuleFor(c => c.NewThreshold)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Threshold must be 0 or greater.")
            .LessThanOrEqualTo(10_000)
            .WithMessage("Threshold must not exceed 10 000.");
    }
}