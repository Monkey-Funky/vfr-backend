
namespace Domain.Enums.Orders;
/// <summary>
/// All valid lifecycle status values for an Order.
/// Stored as varchar(30) in the database.
/// DB CHECK: status IN ('NotProcessed','Processing','Shipped','Delivered','Cancelled')
/// </summary>
public static class OrderStatus
{
    public const string NotProcessed = "NotProcessed";
    public const string Processing = "Processing";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All =
        [NotProcessed, Processing, Shipped, Delivered, Cancelled];

    public static bool IsValid(string value) =>
        value is NotProcessed or Processing or Shipped or Delivered or Cancelled;
}