using Domain.Exceptions;

namespace Domain.Entities.Customer;

public sealed class WardrobeCollection : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private WardrobeCollection() { }

    public static WardrobeCollection Create(Guid customerId, string name)
    {
        if (customerId == Guid.Empty)
            throw new BusinessRuleException("InvalidCustomer", "CustomerId must not be empty.");

        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("InvalidName", "Collection name must not be empty.");

        var trimmedName = name.Trim();
        if (trimmedName.Length > 100)
            throw new BusinessRuleException("NameTooLong", "Collection name cannot exceed 100 characters.");

        return new WardrobeCollection
        {
            CustomerId = customerId,
            Name = trimmedName
        };
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new BusinessRuleException("InvalidName", "Collection name must not be empty.");

        var trimmedName = newName.Trim();
        if (trimmedName.Length > 100)
            throw new BusinessRuleException("NameTooLong", "Collection name cannot exceed 100 characters.");

        Name = trimmedName;
    }

    public void SoftDelete() => MarkAsDeleted();
}