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

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(BaseEntity.CreatedAt)).CurrentValue = DateTime.UtcNow;
                    entry.Property(nameof(BaseEntity.UpdatedAt)).CurrentValue = DateTime.UtcNow;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(BaseEntity.UpdatedAt)).CurrentValue = DateTime.UtcNow;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}