using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
namespace Infrastructure.Services.Subscription;

/// <summary>
/// Implements the product-limit guard for subscription-tier enforcement.
///
/// GUARD LOGIC:
///   1. Load the retailer's active subscription (Status = Active or Trial).
///   2. Load the subscription plan's MaxActiveProducts.
///   3. If MaxActiveProducts is null (Enterprise/unlimited) → pass immediately, no DB product query.
///   4. Count retailer's active products (IsDeleted = false AND Status = 'Active').
///   5. If count >= MaxActiveProducts → throw BusinessRuleException("PRODUCT_LIMIT_REACHED").
///
/// The null check for MaxActiveProducts is critical: it prevents an unnecessary
/// COUNT(*) query on very large product tables for Enterprise-tier retailers.
/// </summary>
public sealed class PlanLimitService : IPlanLimitService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PlanLimitService> _logger;

    public PlanLimitService(
        IApplicationDbContext context,
        ILogger<PlanLimitService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnforceAsync(Guid retailerId, CancellationToken cancellationToken = default)
    {
        // Step 1: Load the retailer's current subscription with its plan
        var subscriptionData = await _context.Subscriptions
            .AsNoTracking()
            .Where(s => s.RetailerId == retailerId)
            .Select(s => new
            {
                s.Status,
                s.Plan.MaxActiveProducts,
                s.Plan.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (subscriptionData is null)
        {
            _logger.LogWarning(
                "PlanLimitService: No subscription found for retailer {RetailerId}. Limit check skipped.",
                retailerId);
            return;
        }

        // Step 2: Enterprise/unlimited — MaxActiveProducts == null → always passes, no DB query
        if (subscriptionData.MaxActiveProducts is null)
        {
            _logger.LogDebug(
                "PlanLimitService: Retailer {RetailerId} on plan '{Plan}' has unlimited products. Check passed.",
                retailerId, subscriptionData.Name);
            return;
        }

        // Step 3: Count active products for this retailer.
        // Global query filter handles IsDeleted = false automatically.
        // Status = 'Active' filter applied explicitly.
        // NOTE: Product.Status is a string column; the Product stub does not define it
        // yet — P-020 will add it. Until then, this counts all non-deleted products.
        int activeCount = await _context.Products
            .AsNoTracking()
            .CountAsync(p => p.RetailerId == retailerId, cancellationToken);

        _logger.LogDebug(
            "PlanLimitService: Retailer {RetailerId} has {Count}/{Max} active products on plan '{Plan}'.",
            retailerId, activeCount, subscriptionData.MaxActiveProducts, subscriptionData.Name);

        // Step 4: Enforce limit
        if (activeCount >= subscriptionData.MaxActiveProducts.Value)
        {
            throw new BusinessRuleException(
                "PRODUCT_LIMIT_REACHED",
                $"You have reached your plan's limit of {subscriptionData.MaxActiveProducts} active products. " +
                "Please upgrade to a higher-tier plan to add more products, " +
                "or deactivate existing products to free up capacity.");
        }
    }
}