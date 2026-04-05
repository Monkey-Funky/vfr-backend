using Domain.Entities.Retailer;
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


    // ── Categories (P-019) ────────────────────────────────────────────────────
    DbSet<Category> Categories { get; }
    DbSet<SubCategory> SubCategories { get; }

    // ── Products (P-019 stub — fully implemented in P-020) ───────────────────
    DbSet<Product> Products { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<InventoryRecord> InventoryRecords { get; }

    // ── Offers (P-019 stub — fully implemented in P-022) ─────────────────────
    DbSet<Offer> Offers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}