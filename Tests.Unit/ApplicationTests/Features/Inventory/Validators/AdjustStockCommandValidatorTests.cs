using Application.Features.Inventory.Commands.AdjustStock;
using Domain.Entities.Retailer;
using FluentValidation.TestHelper;

namespace Tests.Unit.Application.Features.Inventory.Validators;

public sealed class AdjustStockCommandValidatorTests
{
    private readonly AdjustStockCommandValidator _sut = new();

    private static AdjustStockCommand ValidCommand() =>
        new(
            InventoryRecordId: Guid.NewGuid(),
            NewQuantity: 50,
            Type: AdjustmentType.ManualIncrease,
            Reason: "Restocking from supplier"
        );

    [Fact]
    public void Valid_PositiveQuantity_PassesValidation()
    {
        var result = _sut.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Invalid_ZeroQuantity_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { NewQuantity = 0 });
        result.ShouldHaveValidationErrorFor(x => x.NewQuantity)
              .WithErrorMessage("NewQuantity must be greater than zero.");
    }

    [Fact]
    public void Invalid_EmptyReason_FailsWithMessage()
    {
        var command = ValidCommand() with
        {
            Type = AdjustmentType.ManualIncrease,
            Reason = null
        };
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Reason)
              .WithErrorMessage("Reason is required for manual stock adjustments.");
    }
}