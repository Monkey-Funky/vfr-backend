namespace Domain.Enums.Product;

/// <summary>
/// Lifecycle status values for a Product.
/// Stored as varchar(20) in the database.
/// </summary>
public static class ProductStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Draft = "Draft";

    public static readonly IReadOnlyList<string> All = [Active, Inactive, Draft];

    public static bool IsValid(string value) =>
        value is Active or Inactive or Draft;
}