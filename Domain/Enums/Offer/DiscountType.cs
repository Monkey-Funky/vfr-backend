namespace Domain.Enums.Offer;

/// <summary>
/// Determines how the discount value is interpreted.
/// Stored as varchar(20) in the database.
/// DB CHECK: discount_type IN ('Percentage', 'Fixed').
/// </summary>
public static class DiscountType
{
    public const string Percentage = "Percentage";
    public const string Fixed = "Fixed";

    public static readonly IReadOnlyList<string> All = [Percentage, Fixed];

    public static bool IsValid(string value) => value is Percentage or Fixed;
}