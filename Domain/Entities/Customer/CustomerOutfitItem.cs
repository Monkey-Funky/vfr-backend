using Domain.Enums.Customer;

namespace Domain.Entities.Customer;

/// <summary>
/// Represents an item mapped to a specific slot within a CustomerOutfit.
/// </summary>
public sealed class CustomerOutfitItem : BaseEntity
{
    // =========================================================================
    // Properties
    // =========================================================================

    public Guid OutfitId { get; private set; }
    public Guid ProductId { get; private set; }
    public SlotType SlotType { get; private set; }
    public int DisplayOrder { get; private set; }

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private CustomerOutfitItem() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    internal static CustomerOutfitItem Create(Guid outfitId, Guid productId, SlotType slotType, int displayOrder)
    {
        if (outfitId == Guid.Empty)
            throw new ArgumentException("OutfitId cannot be empty.", nameof(outfitId));
            
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));

        return new CustomerOutfitItem
        {
            OutfitId = outfitId,
            ProductId = productId,
            SlotType = slotType,
            DisplayOrder = displayOrder
        };
    }
}
