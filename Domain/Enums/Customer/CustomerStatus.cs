namespace Domain.Enums.Customer;

/// <summary>
/// All valid lifecycle status values for a CustomerAccount.
/// </summary>
public static class CustomerStatus
{
    public const string PendingEmailVerification = "PendingEmailVerification";
    public const string Active = "Active";
    public const string Suspended = "Suspended";
    public const string PendingDeletion = "PendingDeletion";

    public static readonly IReadOnlyList<string> All =
        [PendingEmailVerification, Active, Suspended, PendingDeletion];

    public static bool IsValid(string value) =>
        All.Contains(value);
}
