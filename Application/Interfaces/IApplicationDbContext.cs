using Microsoft.EntityFrameworkCore;

namespace Application.Interfaces;

/// <summary>
/// Abstraction over EF Core DbContext. Application layer only knows this interface —
/// it never references the concrete ApplicationDbContext or any EF Core types.
/// DbSet properties are added here as entities are created in subsequent prompts.
/// </summary>
public interface IApplicationDbContext
{
    // ── Auth / Retailer (added P-011) ─────────────────────────────────────────
    DbSet<RetailerAccount> RetailerAccounts { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }

    // ── Subscriptions (added P-015) ───────────────────────────────────────────
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<SubscriptionPayment> SubscriptionPayments { get; }
    DbSet<SaasEnquiry> SaasEnquiries { get; }


    // ── Payment Methods (added P-016) ─────────────────────────────────────────
    DbSet<PaymentMethod> PaymentMethods { get; }


    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}