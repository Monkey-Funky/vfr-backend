using Domain.Enums.Customer;

namespace Domain.Entities.Customer;

/// <summary>
/// Represents an outfit created by a customer, combining multiple products into slots.
/// </summary>
public sealed class CustomerOutfit : BaseEntity
{
    // =========================================================================
    // Properties
    // =========================================================================

    public Guid CustomerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? StyleCategory { get; private set; }

    private readonly List<CustomerOutfitItem> _items = new();
    public IReadOnlyCollection<CustomerOutfitItem> Items => _items.AsReadOnly();

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private CustomerOutfit() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    public static CustomerOutfit Create(Guid customerId, string name, string? styleCategory = null)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
            
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        return new CustomerOutfit
        {
            CustomerId = customerId,
            Name = name.Trim(),
            StyleCategory = styleCategory?.Trim()
        };
    }

    // =========================================================================
    // Domain Methods
    // =========================================================================

    public void AddOrUpdateItem(Guid productId, SlotType slot, int displayOrder)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));

        // Enforce rule: An outfit cannot have more than one item in the same SlotType (except Accessories).
        if (slot != SlotType.Accessory)
        {
            var existingItemInSlot = _items.FirstOrDefault(i => i.SlotType == slot && !i.IsDeleted);
            if (existingItemInSlot != null)
            {
                if (existingItemInSlot.ProductId == productId)
                    return; // Already in slot
                
                // Remove existing item in the slot
                existingItemInSlot.MarkAsDeleted();
            }
        }
        else
        {
            // For accessories, just ensure we don't duplicate the exact same product
            var existingProduct = _items.FirstOrDefault(i => i.ProductId == productId && !i.IsDeleted);
            if (existingProduct != null)
                return; // Already added
        }

        var newItem = CustomerOutfitItem.Create(Id, productId, slot, displayOrder);
        _items.Add(newItem);
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty.", nameof(newName));

        Name = newName.Trim();
    }

    public void UpdateStyle(string? newStyleCategory)
    {
        StyleCategory = newStyleCategory?.Trim();
    }

    public void ClearItems()
    {
        foreach (var item in _items.Where(i => !i.IsDeleted))
        {
            item.MarkAsDeleted();
        }
    }

    public void SoftDelete()
    {
        MarkAsDeleted();
        foreach (var item in _items.Where(i => !i.IsDeleted))
        {
            item.MarkAsDeleted();
        }
    }
}
