using Application.Common;
using Application.Features.Products.Commands.CreateProduct;
using FluentValidation.TestHelper;

namespace Tests.Unit.Application.Features.Products.Validators;

public sealed class CreateProductCommandValidatorTests
{
    private readonly CreateProductCommandValidator _sut = new();

    private static CreateProductCommand ValidCommand() =>
        new(
            Name: "Classic White Shirt",
            Description: "A timeless white shirt.",
            CategoryId: Guid.NewGuid(),
            SubCategoryId: null,
            Price: 49.99m,
            Currency: "USD",
            Barcode: null,
            InitialQuantity: 10,
            Status: "Active",
            Images: null
        );

    [Fact]
    public void Valid_AllRequiredFields_PassesValidation()
    {
        var result = _sut.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Invalid_EmptyName_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Name = "" });
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Product name is required.");
    }

    [Fact]
    public void Invalid_NegativePrice_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Price = -1m });
        result.ShouldHaveValidationErrorFor(x => x.Price)
              .WithErrorMessage("Price must be greater than zero.");
    }

    [Fact]
    public void Invalid_ZeroPrice_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Price = 0m });
        result.ShouldHaveValidationErrorFor(x => x.Price)
              .WithErrorMessage("Price must be greater than zero.");
    }

    [Fact]
    public void Invalid_DescriptionExceedsMaxLength_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Description = new string('x', 1001) });
        result.ShouldHaveValidationErrorFor(x => x.Description)
              .WithErrorMessage("Description must not exceed 1000 characters.");
    }

    [Fact]
    public void Invalid_EmptyCategoryId_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { CategoryId = null });
        result.ShouldHaveValidationErrorFor(x => x.CategoryId)
              .WithErrorMessage("CategoryId is required.");
    }
}