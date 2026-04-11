namespace Application.Interfaces.Services;

/// <summary>
/// Enforces the plan's active product limit before a product is created or activated.
/// Called by CreateProductCommandHandler and any command that activates a product.
///
/// RULES:
///   - Enterprise tier (MaxActiveProducts == null) always passes without a DB query.
///   - Count query targets ONLY: IsDeleted = false AND Status = 'Active' products.
///   - Raises BusinessRuleException("PRODUCT_LIMIT_REACHED") when limit is exceeded.
///
/// Registered as Scoped. Implemented in Infrastructure.Services.PlanLimitService.
/// </summary>
public interface IPlanLimitService
{
    /// <summary>
    /// Validates that the retailer has not exceeded their plan's active product limit.
    /// </summary>
    /// <param name="retailerId">The retailer whose limit is being checked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="BusinessRuleException">
    /// Thrown with code "PRODUCT_LIMIT_REACHED" when the limit is exceeded.
    /// </exception>
    Task EnforceAsync(Guid retailerId, CancellationToken cancellationToken = default);
}

