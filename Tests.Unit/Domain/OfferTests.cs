using Domain.Entities.Retailer;
using Domain.Exceptions;
using FluentAssertions;


namespace Tests.Unit.Domain;
public sealed class OfferTests
{
    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static Offer BuildOffer(
        decimal discountValue,
        string discountType = "Percentage",
        DateOnly? startDate = null,
        DateOnly? endDate = null)
        => Offer.Create(
            RetailerId,
            "Summer Sale",
            "A great deal",
            "Product",
            ProductId,
            null,
            discountType,
            discountValue,
            startDate ?? Today,
            endDate ?? Today.AddDays(30),
            "https://cdn.example.com/cover.jpg");

    [Fact]
    public void Create_StartDateGreaterThanOrEqualToEndDate_ThrowsBusinessRuleException()
    {
        // Arrange
        var startDate = Today;
        var endDate = Today.AddDays(-1);

        // Act
        var act = () => BuildOffer(10m, startDate: startDate, endDate: endDate);

        // Assert
        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Create_PercentageDiscountOfExactlyZero_ThrowsBusinessRuleException()
    {
        // Arrange
        const decimal discountValue = 0m;

        // Act
        var act = () => BuildOffer(discountValue, discountType: "Percentage");

        // Assert
        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Create_PercentageDiscountOfExactlyOneHundred_ThrowsBusinessRuleException()
    {
        // Arrange
        const decimal discountValue = 100m;

        // Act
        var act = () => BuildOffer(discountValue, discountType: "Percentage");

        // Assert
        act.Should().Throw<BusinessRuleException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    public void Create_PercentageDiscountBetweenOneAndNinetyNine_ThrowsNothing(decimal value)
    {
        // Arrange & Act
        var act = () => BuildOffer(value, discountType: "Percentage");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_FixedDiscountWithNegativeValue_ThrowsBusinessRuleException()
    {
        // Arrange
        const decimal discountValue = -5m;

        // Act
        var act = () => BuildOffer(discountValue, discountType: "Fixed");

        // Assert
        act.Should().Throw<BusinessRuleException>();
    }
}