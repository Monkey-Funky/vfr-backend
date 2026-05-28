using Domain.Enums.Offer;

namespace Tests.Unit.DomainTests.Entities;

public sealed class OfferTests
{
    private static readonly Guid ValidRetailerId = Guid.NewGuid();
    private static readonly Guid ValidProductId = Guid.NewGuid();
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static Offer CreateProductOffer(DateOnly? startDate = null, DateOnly? endDate = null, decimal discountValue = 20m)
        => Offer.Create(
            ValidRetailerId,
            "Summer Sale",
            "Great seasonal deals",
            OfferType.Product,
            ValidProductId,
            null,
            DiscountType.Percentage,
            discountValue,
            startDate ?? Today.AddDays(-1),
            endDate,
            "https://cdn.example.com/summer-sale.jpg");

    [Fact]
    public void Create_ValidParameters_SetsPropertiesCorrectly()
    {
        var retailerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var startDate = Today;

        var offer = Offer.Create(
            retailerId, "Winter Clearance", "Huge discounts",
            OfferType.Product, productId, null,
            DiscountType.Percentage, 30m,
            startDate, null, "https://cdn.example.com/winter.jpg");

        offer.Id.Should().NotBeEmpty();
        offer.RetailerId.Should().Be(retailerId);
        offer.Title.Should().Be("Winter Clearance");
        offer.Description.Should().Be("Huge discounts");
        offer.OfferType.Should().Be(OfferType.Product);
        offer.ProductId.Should().Be(productId);
        offer.CategoryId.Should().BeNull();
        offer.DiscountType.Should().Be(DiscountType.Percentage);
        offer.DiscountValue.Should().Be(30m);
        offer.StartDate.Should().Be(startDate);
        offer.EndDate.Should().BeNull();
        offer.CoverImageUrl.Should().Be("https://cdn.example.com/winter.jpg");
        offer.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_IsActiveByDefault()
    {
        var offer = CreateProductOffer();

        offer.Status.Should().Be(OfferStatus.Active);
    }

    [Fact]
    public void IsExpired_ReturnsTrueWhenEndDatePassed()
    {
        var pastStart = Today.AddDays(-10);
        var pastEnd = Today.AddDays(-1);
        var offer = CreateProductOffer(startDate: pastStart, endDate: pastEnd);

        offer.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsActive_ReturnsFalseWhenNotStartedYet()
    {
        var futureStart = Today.AddDays(5);
        var offer = CreateProductOffer(startDate: futureStart);

        offer.IsActive(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Expire_SetsStatusToInactive()
    {
        var offer = CreateProductOffer();
        offer.Status.Should().Be(OfferStatus.Active);

        offer.Deactivate();

        offer.Status.Should().Be(OfferStatus.Expired);
        offer.IsActive(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void UpdateDiscount_UpdatesPercentage()
    {
        var offer = CreateProductOffer(discountValue: 20m);
        offer.DiscountValue.Should().Be(20m);

        offer.Update(
            "Flash Sale",
            "Limited time offer",
            DiscountType.Percentage,
            35m,
            Today,
            null,
            OfferStatus.Active);

        offer.Title.Should().Be("Flash Sale");
        offer.DiscountType.Should().Be(DiscountType.Percentage);
        offer.DiscountValue.Should().Be(35m);
    }
}