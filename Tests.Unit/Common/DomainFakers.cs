using Bogus;
using Domain.Entities.Orders;
using Domain.Entities.Retailer;
using Domain.Enums.Product;

namespace Tests.Unit.Common;

public sealed class RetailerFaker : Faker<RetailerAccount>
{
    public RetailerFaker()
    {
        CustomInstantiator(f => RetailerAccount.Create(
            f.Person.FullName,
            f.Internet.Email(),
            "$2a$12$fixedHashForTestingPurposesOnly",
            f.Company.CompanyName()));
    }
}

public sealed class CategoryFaker : Faker<Category>
{
    public CategoryFaker()
    {
        CustomInstantiator(f => Category.Create(
            Guid.NewGuid(),
            f.Commerce.Categories(1)[0],
            f.Lorem.Sentence(),
            f.Internet.Url(),
            Category.CategoryStatus.Active));
    }
}

public sealed class ProductFaker : Faker<Product>
{
    public ProductFaker()
    {
        CustomInstantiator(f => Product.Create(
            Guid.NewGuid(),
            f.Commerce.ProductName(),
            f.Lorem.Sentence(),
            null,
            null,
            f.Random.Decimal(10m, 500m),
            "EGP",
            null,
            ProductStatus.Active));
    }
}

public sealed class OfferFaker : Faker<Offer>
{
    public OfferFaker()
    {
        CustomInstantiator(f =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return Offer.Create(
                Guid.NewGuid(),
                f.Commerce.ProductName(),
                f.Lorem.Sentence(),
                "Product",
                Guid.NewGuid(),
                null,
                "Percentage",
                f.Random.Decimal(1m, 49m),
                today,
                today.AddDays(30),
                f.Internet.Url());
        });
    }
}

public sealed class OrderFaker : Faker<Order>
{
    public OrderFaker()
    {
        CustomInstantiator(f =>
        {
            var items = new List<(Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity)>
            {
                (Guid.NewGuid(), f.Commerce.ProductName(), f.Random.Decimal(1m, 200m), f.Random.Int(1, 10))
            };
            return Order.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                f.Person.FullName,
                items);
        });
    }
}

public sealed class InventoryRecordFaker : Faker<InventoryRecord>
{
    public InventoryRecordFaker()
    {
        CustomInstantiator(f => InventoryRecord.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            f.Commerce.ProductName(),
            f.Random.Int(20, 200),
            10));
    }
}