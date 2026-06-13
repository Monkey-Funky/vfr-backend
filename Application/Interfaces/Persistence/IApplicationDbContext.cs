// Application/Interfaces/Persistence/IApplicationDbContext.cs
using Domain.Entities.Analytics;
using Domain.Entities.Notifications;
using Domain.Entities.Orders;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Application.Interfaces.Persistence;

/// <summary>
/// Abstraction over EF Core DbContext. Application layer only knows this interface —
/// it never references the concrete ApplicationDbContext or any EF Core types.
/// </summary>
public interface IApplicationDbContext
{
    // ── Auth (P-011) ───────────────────────────────────────────────
    DbSet<RetailerAccount> RetailerAccounts { get; }
    DbSet<CustomerAccount> CustomerAccounts { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    
    // ── Customer Profile & Avatar (P-Customer) ────────────────────────────────
    DbSet<Avatar> Avatars { get; }
    DbSet<AvatarMeasurementHistory> AvatarMeasurementHistory { get; }
    DbSet<VirtualTryOnSession> VirtualTryOnSessions { get; }
    DbSet<FitFeedback> FitFeedback { get; }
    DbSet<CustomerFavorite> CustomerFavorites { get; }
    DbSet<CustomerOutfit> CustomerOutfits { get; }
    DbSet<CustomerOutfitItem> CustomerOutfitItems { get; }
    DbSet<WardrobeCollection> WardrobeCollections { get; }
    DbSet<WardrobeCollectionItem> WardrobeCollectionItems { get; }

    // ── Customer Ordering ────────────────────────────────
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }

    // ── Subscriptions (P-015) ─────────────────────────────────────────────────
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<SubscriptionPayment> SubscriptionPayments { get; }
    DbSet<SaasEnquiry> SaasEnquiries { get; }

    // ── Payment Methods (P-016) ───────────────────────────────────────────────
    DbSet<PaymentMethod> PaymentMethods { get; }

    // ── Categories (P-019) ────────────────────────────────────────────────────
    DbSet<Category> Categories { get; }
    DbSet<SubCategory> SubCategories { get; }

    // ── Products (P-022) ─────────────────────────────────────────────────────
    DbSet<Product> Products { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<InventoryRecord> InventoryRecords { get; }

    // ── Offers (P-026) ────────────────────────────────────────────────────────
    DbSet<Offer> Offers { get; }

    // ── Orders (P-030) ────────────────────────────────────────────────────────
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<CommissionRecord> CommissionRecords { get; }

    // ── Inventory (P-033) ─────────────────────────────────────────────────────
    DbSet<StockAdjustment> StockAdjustments { get; }

    // ── Notifications (P-039) ─────────────────────────────────────────────────
    DbSet<Notification> Notifications { get; }

    // ── Analytics / Dashboard (P-041) ─────────────────────────────────────────
    DbSet<DashboardSnapshot> DashboardSnapshots { get; }
    DbSet<Domain.Entities.Analytics.ActivityEvent> ActivityEvents { get; }
    DbSet<TryOnSession> TryOnSessions { get; }
    DbSet<VfrEngagementMetric> VfrEngagementMetrics { get; }
    DbSet<ReturnReason> ReturnReasons { get; }
    DbSet<FitAccuracy> FitAccuracies { get; }
    DbSet<Report> Reports { get; }

    // ── EF Core raw SQL surface (P-042) ───────────────────────────────────────
    /// <summary>
    /// Provides access to EF Core's raw SQL execution API (SqlQueryRaw, ExecuteSqlRawAsync).
    /// Required by DashboardRepository for PostgreSQL DATE_TRUNC aggregations.
    /// ApplicationDbContext : DbContext satisfies this property automatically.
    /// </summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}