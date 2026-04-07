namespace Domain.Enums.Analytics;

/// <summary>
/// Enumeration of standardised return reasons.
/// Stored as varchar(50) in the database with a CHECK constraint.
/// </summary>
public enum ReturnReasonType
{
    WrongSize = 0,
    DefectivItem = 1,
    NotAsDescribed = 2,
    ChangedMind = 3,
    LateDelivery = 4,
    DamagedInShipping = 5,
    Other = 6
}