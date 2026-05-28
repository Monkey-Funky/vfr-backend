namespace Application.Features.Inventory.Commands.AdjustStock;

public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    // ManualIncrease and ManualDecrease require an explicit reason.
    private static readonly IReadOnlySet<string> ManualTypes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            AdjustmentType.ManualIncrease,
            AdjustmentType.ManualDecrease
        };

    public AdjustStockCommandValidator()
    {
        RuleFor(c => c.InventoryRecordId)
            .NotEmpty()
            .WithMessage("InventoryRecordId is required.");

        RuleFor(c => c.NewQuantity)
            .GreaterThan(0)
            .WithMessage("NewQuantity must be greater than zero.");

        RuleFor(c => c.Type)
            .NotEmpty()
            .WithMessage("Adjustment type is required.")
            .Must(t => t is
                AdjustmentType.ManualIncrease or
                AdjustmentType.ManualDecrease or
                AdjustmentType.OrderSale or
                AdjustmentType.ReturnRestock)
            .WithMessage(
                $"Type must be one of: {AdjustmentType.ManualIncrease}, " +
                $"{AdjustmentType.ManualDecrease}, {AdjustmentType.OrderSale}, " +
                $"{AdjustmentType.ReturnRestock}.");

        // Reason is required for manual types
        RuleFor(c => c.Reason)
            .NotEmpty()
            .WithMessage("Reason is required for manual stock adjustments.")
            .When(c => ManualTypes.Contains(c.Type ?? string.Empty));

        RuleFor(c => c.Reason)
            .MaximumLength(500)
            .WithMessage("Reason must not exceed 500 characters.")
            .When(c => c.Reason is not null);
    }
}