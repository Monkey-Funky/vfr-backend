using Domain.Enums.Orders;
using Domain.Events;
using Domain.Exceptions;


namespace Domain.Entities.Orders;

/// <summary>
/// Aggregate root representing a customer's order placed through a retailer.
///
/// DESIGN RULES:
///   • All property setters are private — state changes only through domain methods.
///   • IsDeleted is inherited from BaseEntity — DO NOT re-declare it here.
///   • RowVersion is the optimistic concurrency token (integer). EF Core includes
///     it in every UPDATE WHERE clause to prevent lost-update races on concurrent
///     status transitions (see OrderConfiguration for IsConcurrencyToken mapping).
///   • UpdateStatus() enforces an explicit state machine via AllowedTransitions.
///     Invalid transitions throw BusinessRuleException → HTTP 422.
///   • Items are a private-backed collection; callers receive IReadOnlyList.
/// </summary>
public sealed class Order : BaseEntity
{
    // =========================================================================
    // Allowed Status Transitions (State Machine)
    // =========================================================================

    /// <summary>
    /// Explicit finite state machine for order status transitions.
    /// Only transitions listed here are permitted; all others throw BusinessRuleException.
    /// Delivered and Cancelled are terminal states — empty sets.
    /// </summary>
    private static readonly Dictionary<string, IReadOnlySet<string>> AllowedTransitions = new()
    {
        [OrderStatus.NotProcessed] = new HashSet<string> { OrderStatus.Processing, OrderStatus.Cancelled },
        [OrderStatus.Processing] = new HashSet<string> { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped] = new HashSet<string> { OrderStatus.Delivered },
        [OrderStatus.Delivered] = new HashSet<string>(),  // terminal — no further transitions
        [OrderStatus.Cancelled] = new HashSet<string>(),  // terminal — no further transitions
    };

    // =========================================================================
    // Properties
    // =========================================================================

    /// <summary>FK to the owning retailer. Never null after construction.</summary>
    public Guid RetailerId { get; private set; }

    /// <summary>FK to the customer who placed the order (cross-module reference).</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Snapshot of the customer's name at the time the order was placed.
    /// Denormalized — not updated if the customer later changes their name.
    /// </summary>
    public string CustomerName { get; private set; } = string.Empty;

    /// <summary>Timestamp when the order was placed.</summary>
    public DateTime OrderDate { get; private set; }

    /// <summary>Total monetary value of all items in this order.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>ISO 4217 currency code. Defaults to "EGP".</summary>
    public string Currency { get; private set; } = "EGP";

    /// <summary>
    /// Current order lifecycle status. Use <see cref="OrderStatus"/> constants.
    /// Transitions are validated by <see cref="UpdateStatus"/>.
    /// </summary>
    public string Status { get; private set; } = OrderStatus.NotProcessed;

    /// <summary>
    /// Optimistic concurrency token — incremented by EF Core on every UPDATE.
    /// Prevents lost-update races when two requests try to transition status simultaneously.
    /// Mapped via Property(o => o.RowVersion).IsConcurrencyToken() in OrderConfiguration.
    /// </summary>
    public int RowVersion { get; private set; }

    // =========================================================================
    // Navigation
    // =========================================================================

    private readonly List<OrderItem> _items = [];

    /// <summary>
    /// The line items in this order. EF Core populates this via Include.
    /// Returns a read-only view to prevent external mutation.
    /// </summary>
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private Order() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates a new Order aggregate with its line items.
    /// Called inside a transaction in PlaceOrderCommandHandler.
    /// </summary>
    /// <param name="retailerId">Owning retailer ID. Must not be empty.</param>
    /// <param name="customerId">Customer ID. Must not be empty.</param>
    /// <param name="customerName">Snapshot of customer name at order time.</param>
    /// <param name="items">Line items. Must not be empty.</param>
    /// <param name="currency">ISO currency code. Defaults to "EGP".</param>
    public static Order Create(
        Guid retailerId,
        Guid customerId,
        string customerName,
        IReadOnlyList<(Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity)> items,
        string currency = "EGP")
    {
        if (retailerId == Guid.Empty)
            throw new ArgumentException("RetailerId must not be empty.", nameof(retailerId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId must not be empty.", nameof(customerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(customerName, nameof(customerName));
        if (items is null || items.Count == 0)
            throw new ArgumentException("An order must have at least one item.", nameof(items));

        var order = new Order
        {
            RetailerId = retailerId,
            CustomerId = customerId,
            CustomerName = customerName.Trim(),
            OrderDate = DateTime.UtcNow,
            Currency = currency,
            Status = OrderStatus.NotProcessed,
        };

        foreach (var (productId, productName, unitPrice, quantity) in items)
        {
            order._items.Add(OrderItem.Create(order.Id, productId, productName, unitPrice, quantity));
        }

        order.TotalAmount = order._items.Sum(i => i.Total);

        return order;
    }

    // =========================================================================
    // Domain Methods
    // =========================================================================

    /// <summary>
    /// Transitions the order to a new status, enforcing the state machine.
    /// Throws <see cref="BusinessRuleException"/> (→ HTTP 422) for invalid transitions.
    /// </summary>
    /// <param name="newStatus">Target status. Must be a valid <see cref="OrderStatus"/> constant.</param>
    /// <exception cref="BusinessRuleException">
    /// Thrown when <paramref name="newStatus"/> is not reachable from the current status.
    /// Includes a descriptive message listing the allowed transitions.
    /// </exception>
    public void UpdateStatus(string newStatus)
    {
        if (!OrderStatus.IsValid(newStatus))
            throw new BusinessRuleException(
                "ORDER_INVALID_STATUS",
                $"'{newStatus}' is not a recognised order status. " +
                $"Valid values: {string.Join(", ", OrderStatus.All)}.");

        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
        {
            var allowedList = AllowedTransitions.TryGetValue(Status, out var set) && set.Count > 0
                ? string.Join(", ", set)
                : "none (terminal state)";

            throw new BusinessRuleException(
                "ORDER_INVALID_TRANSITION",
                $"Cannot transition order from '{Status}' to '{newStatus}'. " +
                $"Allowed transitions from '{Status}': [{allowedList}].");
        }

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow; // direct field write — EF handles this in SaveChangesAsync too
    }
}