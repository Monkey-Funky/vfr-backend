using Application.Common;
using Application.Features.Offers.Commands.CreateOffer;
using Domain.Enums.Offer;
using FluentValidation.TestHelper;

namespace Tests.Unit.Application.Features.Offers.Validators;

public sealed class CreateOfferCommandValidatorTests
{
    private readonly CreateOfferCommandValidator _sut = new();

    private static FileUploadDto ValidCoverImage() =>
        new(
            Content: new MemoryStream(new byte[512]),
            FileName: "cover.jpg",
            ContentType: "image/jpeg",
            Length: 512
        );

    private static CreateOfferCommand ValidCommand() =>
        new(
            Title: "Summer Sale",
            Description: null,
            OfferType: OfferType.Product,
            ProductId: Guid.NewGuid(),
            CategoryId: null,
            DiscountType: DiscountType.Percentage,
            DiscountValue: 10m,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.Date),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)),
            CoverImage: ValidCoverImage()
        );

    [Fact]
    public void Valid_AllFieldsValid_PassesValidation()
    {
        var result = _sut.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Invalid_EmptyName_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Title = "" });
        result.ShouldHaveValidationErrorFor(x => x.Title)
              .WithErrorMessage("Offer title is required.");
    }

    [Fact]
    public void Invalid_DiscountBelowZero_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { DiscountValue = -1m });
        result.ShouldHaveValidationErrorFor(x => x.DiscountValue)
              .WithErrorMessage("DiscountValue must be greater than zero.");
    }

    [Fact]
    public void Invalid_DiscountAbove100_FailsWithMessage()
    {
        var command = ValidCommand() with
        {
            DiscountType = DiscountType.Percentage,
            DiscountValue = 101m
        };
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.DiscountValue)
              .WithErrorMessage("Percentage discount must be between 1 and 100.");
    }

    [Fact]
    public void Invalid_EndDateBeforeStartDate_FailsWithMessage()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var command = ValidCommand() with
        {
            StartDate = today,
            EndDate = today.AddDays(-1)
        };
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x)
              .WithErrorMessage("End date must be after start date.");
    }

    [Fact]
    public void Invalid_EmptyProductIds_FailsWithMessage()
    {
        var command = ValidCommand() with
        {
            OfferType = OfferType.Product,
            ProductId = null
        };
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ProductId)
              .WithErrorMessage("ProductId is required for a Product-type offer.");
    }
}