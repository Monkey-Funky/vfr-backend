namespace Domain.Enums.Offer;

/// <summary>
/// Indicates whether an offer targets a single product or an entire category.
/// Stored as varchar(20) in the database.
/// DB CHECK: offer_type IN ('Product', 'Category').
/// </summary>
public static class OfferType
{
    public const string Product = "Product";
    public const string Category = "Category";

    public static readonly IReadOnlyList<string> All = [Product, Category];

    public static bool IsValid(string value) => value is Product or Category;
}