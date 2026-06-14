using Application.Interfaces.Persistence;
using Domain.Entities.Analytics;
using Domain.Entities.Customer;
using Domain.Entities.Notifications;
using Domain.Entities.Orders;
using Domain.Entities.Subscriptions;
using Domain.Entities.Retailer;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the VFR Retailer module.
/// </summary>
public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────────

    public DbSet<RetailerAccount> RetailerAccounts => Set<RetailerAccount>();
    public DbSet<CustomerAccount> CustomerAccounts => Set<CustomerAccount>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    public DbSet<Avatar> Avatars => Set<Avatar>();
    public DbSet<AvatarMeasurementHistory> AvatarMeasurementHistory => Set<AvatarMeasurementHistory>();
    public DbSet<VirtualTryOnSession> VirtualTryOnSessions => Set<VirtualTryOnSession>();
    public DbSet<FitFeedback> FitFeedback => Set<FitFeedback>();
    public DbSet<CustomerFavorite> CustomerFavorites => Set<CustomerFavorite>();
    public DbSet<CustomerOutfit> CustomerOutfits => Set<CustomerOutfit>();
    public DbSet<CustomerOutfitItem> CustomerOutfitItems => Set<CustomerOutfitItem>();
    public DbSet<WardrobeCollection> WardrobeCollections => Set<WardrobeCollection>();
    public DbSet<WardrobeCollectionItem> WardrobeCollectionItems => Set<WardrobeCollectionItem>();

    // Cart Service
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }

    // Checkout Service
    public DbSet<Domain.Entities.CustomerOrders.COrder> COrders { get; set; }
    public DbSet<Domain.Entities.CustomerOrders.COrderItem> COrderItems { get; set; }

    // Payment Service
    public DbSet<Domain.Entities.CustomerOrders.Payment> Payments { get; set; }

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();
    public DbSet<SaasEnquiry> SaasEnquiries => Set<SaasEnquiry>();

    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<InventoryRecord> InventoryRecords => Set<InventoryRecord>();

    public DbSet<Offer> Offers => Set<Offer>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<CommissionRecord> CommissionRecords => Set<CommissionRecord>();

    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<DashboardSnapshot> DashboardSnapshots => Set<DashboardSnapshot>();
    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();
    public DbSet<TryOnSession> TryOnSessions => Set<TryOnSession>();
    public DbSet<VfrEngagementMetric> VfrEngagementMetrics => Set<VfrEngagementMetric>();
    public DbSet<ReturnReason> ReturnReasons => Set<ReturnReason>();
    public DbSet<FitAccuracy> FitAccuracies => Set<FitAccuracy>();
    public DbSet<Report> Reports => Set<Report>();

    // ── Model configuration ───────────────────────────────────────────────────

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    // ── Audit timestamp stamping ──────────────────────────────────────────────
    //
    // IMPORTANT: Some entities extend BaseEntity but intentionally omit certain
    // audit columns from their DB schema (e.g. OrderItem, StockAdjustment,
    // Notification, AvatarMeasurementHistory, VirtualTryOnSession all call
    // builder.Ignore(e => e.UpdatedAt) in their EF configurations because those
    // tables have no updated_at column).
    //
    // Calling entry.Property(...) on a property that has been Ignored by EF throws
    // InvalidOperationException at runtime. We therefore use entry.Metadata.FindProperty()
    // — which returns null for unmapped/ignored properties — to guard every access
    // before stamping the value. This makes the auditing loop safe for ALL current
    // and future entities regardless of their individual column mappings.

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Capture once so every entity in this unit of work shares the same timestamp.
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Guard each property: FindProperty returns null when the property
                    // is ignored in EF configuration and must not be accessed.
                    if (entry.Metadata.FindProperty(nameof(BaseEntity.CreatedAt)) is not null)
                        entry.Property(nameof(BaseEntity.CreatedAt)).CurrentValue = now;

                    if (entry.Metadata.FindProperty(nameof(BaseEntity.UpdatedAt)) is not null)
                        entry.Property(nameof(BaseEntity.UpdatedAt)).CurrentValue = now;

                    break;

                case EntityState.Modified:
                    if (entry.Metadata.FindProperty(nameof(BaseEntity.UpdatedAt)) is not null)
                        entry.Property(nameof(BaseEntity.UpdatedAt)).CurrentValue = now;

                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}