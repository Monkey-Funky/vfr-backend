namespace Domain.Entities.Customer;

/// <summary>
/// Represents a shipping or billing address associated with a CustomerAccount.
/// </summary>
public sealed class CustomerAddress : BaseEntity
{
    public Guid CustomerId { get; private set; }
    
    public string Label { get; private set; } = string.Empty; // 'Home', 'Work', etc.
    public string AddressLine1 { get; private set; } = string.Empty;
    public string? AddressLine2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string? StateProvince { get; private set; }
    public string PostalCode { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }

    // Navigation Property
    public CustomerAccount CustomerAccount { get; private set; } = null!;

    private CustomerAddress() { }

    public static CustomerAddress Create(
        Guid customerId,
        string label,
        string addressLine1,
        string? addressLine2,
        string city,
        string? stateProvince,
        string postalCode,
        string country,
        bool isDefault = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label, nameof(label));
        ArgumentException.ThrowIfNullOrWhiteSpace(addressLine1, nameof(addressLine1));
        ArgumentException.ThrowIfNullOrWhiteSpace(city, nameof(city));
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode, nameof(postalCode));
        ArgumentException.ThrowIfNullOrWhiteSpace(country, nameof(country));

        return new CustomerAddress
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Label = label.Trim(),
            AddressLine1 = addressLine1.Trim(),
            AddressLine2 = addressLine2?.Trim(),
            City = city.Trim(),
            StateProvince = stateProvince?.Trim(),
            PostalCode = postalCode.Trim(),
            Country = country.Trim(),
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        string label,
        string addressLine1,
        string? addressLine2,
        string city,
        string? stateProvince,
        string postalCode,
        string country)
    {
        Label = label.Trim();
        AddressLine1 = addressLine1.Trim();
        AddressLine2 = addressLine2?.Trim();
        City = city.Trim();
        StateProvince = stateProvince?.Trim();
        PostalCode = postalCode.Trim();
        Country = country.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
