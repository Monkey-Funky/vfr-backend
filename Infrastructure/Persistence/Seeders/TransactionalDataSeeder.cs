// src/Infrastructure/Persistence/Seeders/TransactionalDataSeeder.cs

namespace Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeds realistic transactional and analytics data for the VFR demo retailer.
///
/// TABLES SEEDED (in dependency order):
///   1. customer_accounts          – 10 realistic customers with Arabic names
///   2. notification_preferences   – preferences record for the seed retailer
///   3. subscriptions              – Active Standard Monthly subscription
///   4. subscription_payments      – 3 months of completed payment history
///   5. offers                     – 3 active promotional offers
///   6. orders                     – 30 orders across 90 days (all statuses)
///   7. order_items                – 1–3 line items per order
///   8. commission_records         – for every Delivered order
///   9. stock_adjustments          – order-sale and manual stock audit trail
///  10. notifications              – 15 retailer in-app notifications
///  11. activity_events            – 50 real-time activity events
///  12. try_on_sessions            – 60 VFR sessions across 90 days
///  13. vfr_engagement_metrics     – 30 daily aggregate metrics
///  14. dashboard_snapshots        – 30 days of nightly KPI snapshots
///
/// DESIGN RULES (same as SubscriptionPlanSeeder):
///   • Idempotent  — all inserts use ON CONFLICT (id) DO NOTHING.
///   • Fixed GUIDs — deterministic; safe to re-run on any environment.
///   • ExecuteSqlAsync(FormattableString) — EF Core 9 parameterized SQL (no boxing).
///   • All timestamps are UTC; dates spread realistically over the last 90 days.
/// </summary>
internal sealed class TransactionalDataSeeder : ISeeder
{
    // ═════════════════════════════════════════════════════════════════════════
    // Fixed GUIDs — single source of truth
    // ═════════════════════════════════════════════════════════════════════════

    // The seed retailer created by ExcelDataSeeder
    private static readonly Guid RetailerId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa001");

    // Subscription plan IDs from SubscriptionPlanSeeder
    private static readonly Guid StandardMonthlyPlanId = new("22222222-2222-2222-2222-222222222001");

    // ── Customers ──────────────────────────────────────────────────────────────
    private static readonly Guid Cust01 = new("f0000001-0000-0000-0000-000000000001");
    private static readonly Guid Cust02 = new("f0000002-0000-0000-0000-000000000002");
    private static readonly Guid Cust03 = new("f0000003-0000-0000-0000-000000000003");
    private static readonly Guid Cust04 = new("f0000004-0000-0000-0000-000000000004");
    private static readonly Guid Cust05 = new("f0000005-0000-0000-0000-000000000005");
    private static readonly Guid Cust06 = new("f0000006-0000-0000-0000-000000000006");
    private static readonly Guid Cust07 = new("f0000007-0000-0000-0000-000000000007");
    private static readonly Guid Cust08 = new("f0000008-0000-0000-0000-000000000008");
    private static readonly Guid Cust09 = new("f0000009-0000-0000-0000-000000000009");
    private static readonly Guid Cust10 = new("f0000010-0000-0000-0000-000000000010");

    // ── Subscription & Payments ────────────────────────────────────────────────
    private static readonly Guid SubscriptionId = new("a1000001-0000-0000-0000-000000000001");
    private static readonly Guid Payment01Id = new("a2000001-0000-0000-0000-000000000001");
    private static readonly Guid Payment02Id = new("a2000002-0000-0000-0000-000000000002");
    private static readonly Guid Payment03Id = new("a2000003-0000-0000-0000-000000000003");

    // ── Offers ─────────────────────────────────────────────────────────────────
    private static readonly Guid Offer01Id = new("b1000001-0000-0000-0000-000000000001");
    private static readonly Guid Offer02Id = new("b1000002-0000-0000-0000-000000000002");
    private static readonly Guid Offer03Id = new("b1000003-0000-0000-0000-000000000003");

    // ── Notification Preference ────────────────────────────────────────────────
    private static readonly Guid NotifPrefId = new("c1000001-0000-0000-0000-000000000001");

    // ── Existing seeded products (from ExcelDataSeeder — cccccccc pattern) ─────
    private static Guid Prod(int n) =>
        new($"cccccccc-cccc-cccc-cccc-cccc{n:D8}");

    // ── Orders (30 orders) ─────────────────────────────────────────────────────
    private static Guid Ord(int n) =>
        new($"d0000000-0000-0000-0000-{n:D12}");

    // ── Order Items ────────────────────────────────────────────────────────────
    private static Guid Item(int n) =>
        new($"d1000000-0000-0000-0000-{n:D12}");

    // ── Commission Records ─────────────────────────────────────────────────────
    private static Guid Com(int n) =>
        new($"d2000000-0000-0000-0000-{n:D12}");

    // ── Stock Adjustments ──────────────────────────────────────────────────────
    private static Guid Adj(int n) =>
        new($"d3000000-0000-0000-0000-{n:D12}");

    // ── Notifications ──────────────────────────────────────────────────────────
    private static Guid Notif(int n) =>
        new($"d4000000-0000-0000-0000-{n:D12}");

    // ── Activity Events ────────────────────────────────────────────────────────
    private static Guid Act(int n) =>
        new($"d5000000-0000-0000-0000-{n:D12}");

    // ── Try-On Sessions ────────────────────────────────────────────────────────
    private static Guid Tryon(int n) =>
        new($"d6000000-0000-0000-0000-{n:D12}");

    // ── VFR Engagement Metrics ────────────────────────────────────────────────
    private static Guid Metric(int n) =>
        new($"d7000000-0000-0000-0000-{n:D12}");

    // ── Dashboard Snapshots ────────────────────────────────────────────────────
    private static Guid Snap(int n) =>
        new($"d8000000-0000-0000-0000-{n:D12}");

    // ── Inventory record IDs (eeeeeeee pattern from ExcelDataSeeder) ──────────
    private static Guid Inv(int n) =>
        new($"eeeeeeee-eeee-eeee-eeee-eeee{n:D8}");

    // ═════════════════════════════════════════════════════════════════════════
    // Dependencies
    // ═════════════════════════════════════════════════════════════════════════

    private readonly ApplicationDbContext _db;
    private readonly ILogger<TransactionalDataSeeder> _log;

    public TransactionalDataSeeder(
        ApplicationDbContext db,
        ILogger<TransactionalDataSeeder> log)
    {
        _db = db;
        _log = log;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Entry point
    // ═════════════════════════════════════════════════════════════════════════

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Fast-path: if subscription already exists assume fully seeded.
        bool alreadySeeded = await _db.Database
            .SqlQuery<int>($"SELECT 1 AS \"Value\" FROM subscriptions WHERE id = {SubscriptionId}")
            .AnyAsync(ct);

        if (alreadySeeded)
        {
            _log.LogDebug("TransactionalDataSeeder: data already present — skipping.");
            return;
        }

        _log.LogInformation("TransactionalDataSeeder: beginning transactional data seed…");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await SeedCustomersAsync(ct);
            await SeedNotificationPreferenceAsync(ct);
            await SeedSubscriptionAsync(ct);
            await SeedSubscriptionPaymentsAsync(ct);
            await SeedOffersAsync(ct);
            await SeedOrdersAndItemsAsync(ct);
            await SeedCommissionRecordsAsync(ct);
            await SeedStockAdjustmentsAsync(ct);
            await SeedNotificationsAsync(ct);
            await SeedActivityEventsAsync(ct);
            await SeedTryOnSessionsAsync(ct);
            await SeedVfrEngagementMetricsAsync(ct);
            await SeedDashboardSnapshotsAsync(ct);

            await tx.CommitAsync(ct);
            _log.LogInformation("TransactionalDataSeeder: all data seeded successfully.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.LogError(ex, "TransactionalDataSeeder: seeding failed — transaction rolled back.");
            throw;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 1. customer_accounts
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SeedCustomersAsync(CancellationToken ct)
    {
        // BCrypt hash of "Customer@2026" — safe placeholder for seed accounts
        const string PwHash = "$2a$11$dummySeedHashForDemoCustomers.xxxxxxxxxxxxxxxxxxxxxxx";
        var now = DateTime.UtcNow;

        var customers = new[]
        {
            (Cust01, "Nour Ahmed Hassan",    "nour.ahmed@gmail.com",        "+201001234501", "Female", new DateOnly(1995,  3, 14)),
            (Cust02, "Laila Mohamed Omar",   "laila.omar@hotmail.com",      "+201001234502", "Female", new DateOnly(1992,  7, 28)),
            (Cust03, "Sara Ibrahim Khalil",  "sara.ibrahim@yahoo.com",      "+201001234503", "Female", new DateOnly(1998,  1,  5)),
            (Cust04, "Maryam Youssef Ali",   "maryam.youssef@gmail.com",    "+201001234504", "Female", new DateOnly(1990, 11, 22)),
            (Cust05, "Hana Mahmoud Saad",    "hana.mahmoud@outlook.com",    "+201001234505", "Female", new DateOnly(1997,  6,  9)),
            (Cust06, "Rania Khaled Farouk",  "rania.khaled@gmail.com",      "+201001234506", "Female", new DateOnly(1993,  4, 17)),
            (Cust07, "Dina Tarek Mostafa",   "dina.tarek@gmail.com",        "+201001234507", "Female", new DateOnly(1996,  9,  3)),
            (Cust08, "Yasmine Adel Nasser",  "yasmine.adel@hotmail.com",    "+201001234508", "Female", new DateOnly(2000,  2, 11)),
            (Cust09, "Amira Hassan Zaki",    "amira.hassan@gmail.com",      "+201001234509", "Female", new DateOnly(1994,  8, 26)),
            (Cust10, "Mariam Samer Lotfy",   "mariam.samer@outlook.com",    "+201001234510", "Female", new DateOnly(1991, 12,  1)),
        };

        foreach (var (id, name, email, phone, gender, dob) in customers)
        {
            var regDate = now.AddDays(-Random.Shared.Next(60, 120));
            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO customer_accounts
                    (id, full_name, email, password_hash, phone_number, date_of_birth,
                     gender, is_email_verified, status, failed_login_attempts,
                     remember_me, is_deleted, created_at, updated_at)
                VALUES
                    ({id}, {name}, {email}, {PwHash}, {phone}, {dob},
                     {gender}, true, 'Active', 0,
                     false, false, {regDate}, {regDate})
                ON CONFLICT (id) DO NOTHING
                """, ct);
        }

        _log.LogDebug("TransactionalDataSeeder: customers seeded.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 2. notification_preferences
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SeedNotificationPreferenceAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await _db.Database.ExecuteSqlAsync($"""
            INSERT INTO notification_preferences
                (id, retailer_id, low_stock_alerts, order_status_alerts,
                 subscription_alerts, email_notifications, in_app_notifications,
                 created_at, updated_at)
            VALUES
                ({NotifPrefId}, {RetailerId}, true, true, true, true, true,
                 {now}, {now})
            ON CONFLICT (id) DO NOTHING
            """, ct);

        _log.LogDebug("TransactionalDataSeeder: notification preferences seeded.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 3. subscriptions
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SeedSubscriptionAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var startDate = now.AddMonths(-3);
        var endDate = now.AddMonths(1);

        // Status stored as string (HasConversion<string>() in EF config)
        const string statusActive = "Active";

        await _db.Database.ExecuteSqlAsync($"""
            INSERT INTO subscriptions
                (id, retailer_id, plan_id, status,
                 start_date, end_date, trial_ends_at,
                 pending_downgrade_plan_id, pending_downgrade_eff_at,
                 is_recurring_enabled, created_at, updated_at)
            VALUES
                ({SubscriptionId}, {RetailerId}, {StandardMonthlyPlanId}, {statusActive},
                 {startDate}, {endDate}, null,
                 null, null,
                 true, {startDate}, {now})
            ON CONFLICT (id) DO NOTHING
            """, ct);

        _log.LogDebug("TransactionalDataSeeder: subscription seeded.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 4. subscription_payments
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SeedSubscriptionPaymentsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Three monthly payment records — Completed status stored as string
        const string statusCompleted = "Completed";
        var payments = new[]
        {
            (Payment01Id, now.AddMonths(-3), now.AddMonths(-2), "pi_seed_001_jan"),
            (Payment02Id, now.AddMonths(-2), now.AddMonths(-1), "pi_seed_002_feb"),
            (Payment03Id, now.AddMonths(-1), now,               "pi_seed_003_mar"),
        };

        foreach (var (pid, periodStart, periodEnd, stripeId) in payments)
        {
            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO subscription_payments
                    (id, retailer_id, subscription_plan_id, payment_method_id,
                     amount, currency, status, is_recurring,
                     period_start_date, period_end_date,
                     stripe_payment_intent_id, paid_at, created_at)
                VALUES
                    ({pid}, {RetailerId}, {StandardMonthlyPlanId}, null,
                     150.00, 'USD', {statusCompleted}, true,
                     {periodStart}, {periodEnd},
                     {stripeId}, {periodEnd.AddDays(-1)}, {periodStart})
                ON CONFLICT (id) DO NOTHING
                """, ct);
        }

        _log.LogDebug("TransactionalDataSeeder: subscription payments seeded.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 5. offers
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SeedOffersAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        // Category-type offer (Outerwear — category bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004)
        var outerwearCatId = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb004");
        await _db.Database.ExecuteSqlAsync($"""
            INSERT INTO offers
                (id, retailer_id, title, description, offer_type,
                 product_id, category_id, discount_type, discount_value,
                 start_date, end_date, cover_image_url, status,
                 is_deleted, created_at, updated_at)
            VALUES
                ({Offer01Id}, {RetailerId},
                 'تخفيض موسم الخريف — Autumn Outerwear Sale',
                 'Get 20% off all jackets and coats. Limited time offer for the new season.',
                 'Category',
                 null, {outerwearCatId}, 'Percentage', 20.00,
                 {today.AddDays(-10)}, {today.AddDays(20)},
                 'https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/13_uc9omt.jpg',
                 'Active',
                 false, {now.AddDays(-10)}, {now.AddDays(-10)})
            ON CONFLICT (id) DO NOTHING
            """, ct);

        // Product-type offer (Product #13 — leather jacket)
        var prodLeatherJacket = Prod(13);
        await _db.Database.ExecuteSqlAsync($"""
            INSERT INTO offers
                (id, retailer_id, title, description, offer_type,
                 product_id, category_id, discount_type, discount_value,
                 start_date, end_date, cover_image_url, status,
                 is_deleted, created_at, updated_at)
            VALUES
                ({Offer02Id}, {RetailerId},
                 'عرض الجاكيت الجلد — Leather Jacket Flash Deal',
                 'Special 15% discount on our best-selling black leather cafe racer jacket.',
                 'Product',
                 {prodLeatherJacket}, null, 'Percentage', 15.00,
                 {today.AddDays(-5)}, {today.AddDays(10)},
                 'https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/13_uc9omt.jpg',
                 'Active',
                 false, {now.AddDays(-5)}, {now.AddDays(-5)})
            ON CONFLICT (id) DO NOTHING
            """, ct);

        // Category-type offer (Dresses — bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002)
        var dressesCatId = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb002");
        await _db.Database.ExecuteSqlAsync($"""
            INSERT INTO offers
                (id, retailer_id, title, description, offer_type,
                 product_id, category_id, discount_type, discount_value,
                 start_date, end_date, cover_image_url, status,
                 is_deleted, created_at, updated_at)
            VALUES
                ({Offer03Id}, {RetailerId},
                 'تشكيلة الفساتين — Dress Collection Special',
                 'EGP 50 off all maxi and midi dresses in our latest collection.',
                 'Category',
                 null, {dressesCatId}, 'Fixed', 50.00,
                 {today}, null,
                 'https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/56_wtsghs.jpg',
                 'Active',
                 false, {now}, {now})
            ON CONFLICT (id) DO NOTHING
            """, ct);

        _log.LogDebug("TransactionalDataSeeder: offers seeded.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 6. orders + order_items
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 30 orders across 90 days:
    ///   Orders  1–5   → Delivered  (oldest, 70–90 days ago)
    ///   Orders  6–10  → Delivered  (50–70 days ago)
    ///   Orders 11–12  → Delivered  (30–50 days ago)
    ///   Orders 13–14  → Cancelled
    ///   Orders 15–19  → Delivered  (10–30 days ago)
    ///   Orders 20–24  → Shipped
    ///   Orders 25–27  → Processing
    ///   Orders 28–30  → NotProcessed (very recent)
    /// </summary>
    private async Task SeedOrdersAndItemsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var orders = BuildOrderSeedRows(now);
        int itemSeq = 1;

        foreach (var o in orders)
        {
            // ── Insert order ──────────────────────────────────────────────────
            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO orders
                    (id, retailer_id, customer_id, customer_name,
                     order_date, total_amount, currency, status,
                     row_version, is_deleted, created_at, updated_at)
                VALUES
                    ({o.Id}, {RetailerId}, {o.CustomerId}, {o.CustomerName},
                     {o.OrderDate}, {o.TotalAmount}, 'EGP', {o.Status},
                     0, false, {o.OrderDate}, {o.UpdatedAt})
                ON CONFLICT (id) DO NOTHING
                """, ct);

            // ── Insert order items ────────────────────────────────────────────
            foreach (var item in o.Items)
            {
                var itemId = Item(itemSeq++);
                var itemTotal = item.UnitPrice * item.Quantity;

                await _db.Database.ExecuteSqlAsync($"""
                    INSERT INTO order_items
                        (id, order_id, product_id, product_name,
                         unit_price, quantity, total, created_at)
                    VALUES
                        ({itemId}, {o.Id}, {item.ProductId}, {item.ProductName},
                         {item.UnitPrice}, {item.Quantity}, {itemTotal}, {o.OrderDate})
                    ON CONFLICT (id) DO NOTHING
                    """, ct);
            }
        }

        _log.LogDebug("TransactionalDataSeeder: {Count} orders seeded.", orders.Count);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 7. commission_records  (3% of order total for Standard plan)
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SeedCommissionRecordsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var orders = BuildOrderSeedRows(now)
            .Where(o => o.Status == "Delivered")
            .ToList();

        int seq = 1;
        foreach (var o in orders)
        {
            var comId = Com(seq++);
            var commRate = 0.0300m;
            var commAmount = Math.Round(o.TotalAmount * commRate, 2);
            var deliveredAt = o.UpdatedAt;   // UpdatedAt is when status reached Delivered

            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO commission_records
                    (id, retailer_id, order_id, subscription_plan_id,
                     commission_rate, order_total, commission_amount,
                     currency, delivered_at, created_at)
                VALUES
                    ({comId}, {RetailerId}, {o.Id}, {StandardMonthlyPlanId},
                     {commRate}, {o.TotalAmount}, {commAmount},
                     'EGP', {deliveredAt}, {deliveredAt})
                ON CONFLICT (id) DO NOTHING
                """, ct);
        }

        _log.LogDebug("TransactionalDataSeeder: commission records seeded.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 8. stock_adjustments  (OrderSale entries for delivered + 5 manual)
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SeedStockAdjustmentsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        int seq = 1;

        // Order-sale adjustments for the first 12 delivered orders (one per order)
        var deliveredOrders = BuildOrderSeedRows(now)
            .Where(o => o.Status == "Delivered")
            .Take(12)
            .ToList();

        foreach (var o in deliveredOrders)
        {
            // Pick the first item's product to generate stock adjustment
            var firstItem = o.Items[0];
            var invId = InventoryIdFromProductIndex(firstItem.ProductId);
            int qty = firstItem.Quantity;
            int oldStock = 60 + seq * 2;        // synthetic old stock
            int newStock = oldStock - qty;

            var reason = "Auto-decremented on order delivery — order #" + o.Id.ToString()[..8];
            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO stock_adjustments
                    (id, inventory_record_id, adjustment_type,
                     old_quantity, new_quantity, reason,
                     adjusted_by_id, adjusted_at)
                VALUES
                    ({Adj(seq)}, {invId}, 'OrderSale',
                     {oldStock}, {newStock}, {reason},
                     {RetailerId}, {o.UpdatedAt})
                ON CONFLICT (id) DO NOTHING
                """, ct);
            seq++;
        }

        // 5 manual stock increase adjustments (restocking events)
        var manualRestocks = new[]
        {
            (Inv(5),  45, 150, "Seasonal restock — Supplier batch #SR-2026-001",  now.AddDays(-55)),
            (Inv(12), 30, 120, "Restock from warehouse — Batch WH-2026-014",      now.AddDays(-40)),
            (Inv(25), 20,  80, "Emergency restock — high demand detected",         now.AddDays(-28)),
            (Inv(37), 15,  75, "Standard monthly restock — invoice INV-2026-033", now.AddDays(-14)),
            (Inv(50),  8,  88, "Restock after stock audit reconciliation",          now.AddDays(-7)),
        };

        foreach (var (invId, oldQty, newQty, reason, adjustedAt) in manualRestocks)
        {
            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO stock_adjustments
                    (id, inventory_record_id, adjustment_type,
                     old_quantity, new_quantity, reason,
                     adjusted_by_id, adjusted_at)
                VALUES
                    ({Adj(seq)}, {invId}, 'ManualIncrease',
                     {oldQty}, {newQty}, {reason},
                     {RetailerId}, {adjustedAt})
                ON CONFLICT (id) DO NOTHING
                """, ct);
            seq++;
        }

        _log.LogDebug("TransactionalDataSeeder: stock adjustments seeded.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 9. notifications
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SeedNotificationsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var notifications = new[]
        {
            // Unread — recent
            (Notif(1),  "LowStock",            "⚠️ Low Stock Alert",
             "Product '013 - Leather Jacket' has dropped to 4 units — below your threshold of 10.",
             (Guid?)Prod(13), false, now.AddHours(-2)),

            (Notif(2),  "NewOrder",             "🛒 New Order Received",
             "Order from Nour Ahmed Hassan for EGP 489.98 is awaiting processing.",
             Ord(30), false, now.AddHours(-5)),

            (Notif(3),  "LowStock",            "⚠️ Low Stock Alert",
             "Product '026 - Wide-Leg Trousers (Off-White)' has 6 units remaining.",
             Prod(26), false, now.AddDays(-1)),

            (Notif(4),  "OrderStatusChanged",   "📦 Order Shipped",
             "Order #" + Ord(24).ToString()[..8] + " has been marked as Shipped.",
             Ord(24), false, now.AddDays(-2)),

            (Notif(5),  "NewOrder",             "🛒 New Order Received",
             "Order from Laila Mohamed Omar for EGP 639.97 is awaiting processing.",
             Ord(29), false, now.AddDays(-2)),

            // Read — older
            (Notif(6),  "OrderStatusChanged",   "✅ Order Delivered",
             "Order #" + Ord(19).ToString()[..8] + " was successfully delivered.",
             Ord(19), true, now.AddDays(-8)),

            (Notif(7),  "SubscriptionExpiring",  "🔔 Subscription Renewal Reminder",
             "Your Standard Monthly plan renews in 7 days. Ensure your payment method is up to date.",
             null, true, now.AddDays(-10)),

            (Notif(8),  "NewOrder",             "🛒 New Order Received",
             "Order from Sara Ibrahim Khalil for EGP 369.98 has been placed.",
             Ord(27), true, now.AddDays(-12)),

            (Notif(9),  "LowStock",            "⚠️ Low Stock Alert",
             "Product '037 - Dark Indigo Wide-Leg Jeans' is running low — only 8 units left.",
             Prod(37), true, now.AddDays(-15)),

            (Notif(10), "OrderStatusChanged",   "❌ Order Cancelled",
             "Order #" + Ord(14).ToString()[..8] + " was cancelled by the customer.",
             Ord(14), true, now.AddDays(-18)),

            (Notif(11), "OrderStatusChanged",   "✅ Order Delivered",
             "Order #" + Ord(15).ToString()[..8] + " was delivered successfully.",
             Ord(15), true, now.AddDays(-20)),

            (Notif(12), "SystemAlert",          "🔧 Platform Maintenance Completed",
             "Scheduled maintenance has been completed. All services are now operating normally.",
             null, true, now.AddDays(-22)),

            (Notif(13), "OrderStatusChanged",   "📦 Order Shipped",
             "Order #" + Ord(12).ToString()[..8] + " has been dispatched via Aramex.",
             Ord(12), true, now.AddDays(-30)),

            (Notif(14), "PaymentFailed",         "💳 Payment Confirmed",
             "Your Standard Monthly subscription payment of USD 150.00 has been confirmed.",
             null, true, now.AddDays(-35)),

            (Notif(15), "NewOrder",             "🛒 New Order Received",
             "Order from Maryam Youssef Ali for EGP 829.97 has been placed.",
             Ord(10), true, now.AddDays(-52)),
        };

        foreach (var (nid, type, title, body, resourceId, isRead, createdAt) in notifications)
        {
            DateTime? readAt = isRead ? createdAt.AddHours(Random.Shared.Next(1, 24)) : (DateTime?)null;

            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO notifications
                    (id, retailer_id, type, title, body,
                     is_read, read_at, resource_id, created_at)
                VALUES
                    ({nid}, {RetailerId}, {type}, {title}, {body},
                     {isRead}, {readAt}, {resourceId}, {createdAt})
                ON CONFLICT (id) DO NOTHING
                """, ct);
        }

        _log.LogDebug("TransactionalDataSeeder: notifications seeded.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 10. activity_events
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SeedActivityEventsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        int seq = 1;

        // Helper to insert one event
        async Task Evt(string type, Guid? resourceId, string data, DateTime at)
        {
            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO activity_events
                    (id, retailer_id, event_type, resource_id, event_data, created_at)
                VALUES
                    ({Act(seq++)}, {RetailerId}, {type}, {resourceId}, {data}::jsonb, {at})
                ON CONFLICT (id) DO NOTHING
                """, ct);
        }

        // --- Recent events (last 7 days) ---
        await Evt("OrderPlaced", Ord(30), $$$"""{"customerName":"Nour Ahmed Hassan","total":489.98,"currency":"EGP"}""", now.AddHours(-5));
        await Evt("OrderPlaced", Ord(29), $$$"""{"customerName":"Laila Mohamed Omar","total":639.97,"currency":"EGP"}""", now.AddHours(-8));
        await Evt("OrderPlaced", Ord(28), $$$"""{"customerName":"Sara Ibrahim Khalil","total":189.99,"currency":"EGP"}""", now.AddDays(-1));
        await Evt("StockAdjusted", Inv(50), $$$"""{"type":"ManualIncrease","productName":"Pleated Midi Skirt","oldQty":8,"newQty":88}""", now.AddDays(-1));
        await Evt("OrderStatusChange", Ord(27), $$$"""{"from":"NotProcessed","to":"Processing","customerName":"Dina Tarek Mostafa"}""", now.AddDays(-2));
        await Evt("OrderPlaced", Ord(27), $$$"""{"customerName":"Dina Tarek Mostafa","total":369.98,"currency":"EGP"}""", now.AddDays(-3));
        await Evt("OrderStatusChange", Ord(26), $$$"""{"from":"NotProcessed","to":"Processing","customerName":"Hana Mahmoud Saad"}""", now.AddDays(-3));
        await Evt("OrderPlaced", Ord(26), $$$"""{"customerName":"Hana Mahmoud Saad","total":559.97,"currency":"EGP"}""", now.AddDays(-4));
        await Evt("LowStockAlert", Prod(13), $$$"""{"productName":"Leather Jacket","currentStock":4,"threshold":10}""", now.AddDays(-4));

        // --- Last 2 weeks ---
        await Evt("OrderStatusChange", Ord(24), $$$"""{"from":"Processing","to":"Shipped","customerName":"Yasmine Adel Nasser"}""", now.AddDays(-5));
        await Evt("OrderStatusChange", Ord(25), $$$"""{"from":"Processing","to":"Shipped","customerName":"Rania Khaled Farouk"}""", now.AddDays(-5));
        await Evt("StockAdjusted", Inv(37), $$$"""{"type":"ManualIncrease","productName":"Dark Indigo Wide-Leg Jeans","oldQty":15,"newQty":75}""", now.AddDays(-7));
        await Evt("OrderStatusChange", Ord(23), $$$"""{"from":"Processing","to":"Shipped","customerName":"Amira Hassan Zaki"}""", now.AddDays(-7));
        await Evt("OrderStatusChange", Ord(22), $$$"""{"from":"Processing","to":"Shipped","customerName":"Mariam Samer Lotfy"}""", now.AddDays(-8));
        await Evt("OrderStatusChange", Ord(21), $$$"""{"from":"Processing","to":"Shipped","customerName":"Nour Ahmed Hassan"}""", now.AddDays(-9));
        await Evt("OrderStatusChange", Ord(20), $$$"""{"from":"Processing","to":"Shipped","customerName":"Maryam Youssef Ali"}""", now.AddDays(-9));
        await Evt("ProductUpdated", Prod(37), $$$"""{"productName":"Dark Indigo Wide-Leg Jeans","field":"Price","oldValue":199.99,"newValue":219.99}""", now.AddDays(-10));

        // --- 2–4 weeks ago ---
        await Evt("OrderStatusChange", Ord(19), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Laila Mohamed Omar"}""", now.AddDays(-11));
        await Evt("OrderStatusChange", Ord(18), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Sara Ibrahim Khalil"}""", now.AddDays(-12));
        await Evt("LowStockAlert", Prod(26), $$$"""{"productName":"Wide-Leg Trousers Off-White","currentStock":6,"threshold":10}""", now.AddDays(-13));
        await Evt("OrderStatusChange", Ord(17), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Hana Mahmoud Saad"}""", now.AddDays(-14));
        await Evt("StockAdjusted", Inv(25), $$$"""{"type":"ManualIncrease","productName":"Statement Top Brown","oldQty":20,"newQty":80}""", now.AddDays(-14));
        await Evt("OrderStatusChange", Ord(16), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Rania Khaled Farouk"}""", now.AddDays(-17));
        await Evt("OrderStatusChange", Ord(15), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Dina Tarek Mostafa"}""", now.AddDays(-20));
        await Evt("OrderCancelled", Ord(14), $$$"""{"customerName":"Yasmine Adel Nasser","reason":"CustomerRequest","total":249.99}""", now.AddDays(-21));
        await Evt("OrderCancelled", Ord(13), $$$"""{"customerName":"Amira Hassan Zaki","reason":"OutOfStock","total":179.99}""", now.AddDays(-23));

        // --- 1–2 months ago ---
        await Evt("OrderStatusChange", Ord(12), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Mariam Samer Lotfy"}""", now.AddDays(-30));
        await Evt("OrderStatusChange", Ord(11), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Nour Ahmed Hassan"}""", now.AddDays(-33));
        await Evt("StockAdjusted", Inv(12), $$$"""{"type":"ManualIncrease","productName":"CK Striped Mock-Neck","oldQty":30,"newQty":120}""", now.AddDays(-40));
        await Evt("OrderStatusChange", Ord(10), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Maryam Youssef Ali"}""", now.AddDays(-42));
        await Evt("OrderStatusChange", Ord(9), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Laila Mohamed Omar"}""", now.AddDays(-45));
        await Evt("OrderStatusChange", Ord(8), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Sara Ibrahim Khalil"}""", now.AddDays(-48));
        await Evt("ProductCreated", Prod(53), $$$"""{"productName":"Oversized Denim Jacket Light Wash","categoryName":"Outerwear"}""", now.AddDays(-50));
        await Evt("OrderStatusChange", Ord(7), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Hana Mahmoud Saad"}""", now.AddDays(-52));
        await Evt("StockAdjusted", Inv(5), $$$"""{"type":"ManualIncrease","productName":"Two-Piece White Blazer Set","oldQty":45,"newQty":150}""", now.AddDays(-55));

        // --- 2–3 months ago ---
        await Evt("OrderStatusChange", Ord(6), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Rania Khaled Farouk"}""", now.AddDays(-58));
        await Evt("LoginSuccess", null, $$$"""{"device":"Chrome on Windows","ip":"197.48.x.x","location":"Cairo, Egypt"}""", now.AddDays(-60));
        await Evt("OrderStatusChange", Ord(5), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Dina Tarek Mostafa"}""", now.AddDays(-63));
        await Evt("OrderStatusChange", Ord(4), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Yasmine Adel Nasser"}""", now.AddDays(-68));
        await Evt("OrderStatusChange", Ord(3), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Amira Hassan Zaki"}""", now.AddDays(-72));
        await Evt("OrderStatusChange", Ord(2), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Mariam Samer Lotfy"}""", now.AddDays(-78));
        await Evt("OrderStatusChange", Ord(1), $$$"""{"from":"Shipped","to":"Delivered","customerName":"Nour Ahmed Hassan"}""", now.AddDays(-83));
        await Evt("ProductCreated", Prod(1), $$$"""{"productName":"Asymmetrical Wrap Black Blazer","categoryName":"Outerwear"}""", now.AddDays(-88));
        await Evt("LoginSuccess", null, $$$"""{"device":"Safari on iPhone","ip":"197.48.x.x","location":"Alexandria, Egypt"}""", now.AddDays(-89));
        await Evt("ProfileUpdated", RetailerId, $$$"""{"field":"brandName","oldValue":"VFR Fashion","newValue":"VFR Fashion House"}""", now.AddDays(-90));

        _log.LogDebug("TransactionalDataSeeder: activity events seeded.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 11. try_on_sessions
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SeedTryOnSessionsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var customers = new[] { Cust01, Cust02, Cust03, Cust04, Cust05,
                                Cust06, Cust07, Cust08, Cust09, Cust10 };

        // 60 sessions spread over 90 days — 30% conversion rate
        var sessions = new List<(int idx, Guid custId, Guid prodId, int durSec, bool purchased, int daysAgo)>
        {
            // Purchased sessions (18 = 30%)
            (1,  Cust01, Prod(1),  185, true,  2),
            (2,  Cust02, Prod(13), 240, true,  3),
            (3,  Cust03, Prod(26), 210, true,  5),
            (4,  Cust04, Prod(37), 195, true,  7),
            (5,  Cust05, Prod(42), 220, true,  9),
            (6,  Cust06, Prod(50), 260, true,  11),
            (7,  Cust07, Prod(56), 175, true,  14),
            (8,  Cust08, Prod(65), 230, true,  17),
            (9,  Cust09, Prod(75), 200, true,  20),
            (10, Cust10, Prod(86), 215, true,  24),
            (11, Cust01, Prod(97), 245, true,  28),
            (12, Cust02, Prod(14), 190, true,  32),
            (13, Cust03, Prod(23), 205, true,  38),
            (14, Cust04, Prod(32), 235, true,  45),
            (15, Cust05, Prod(47), 180, true,  52),
            (16, Cust06, Prod(60), 255, true,  60),
            (17, Cust07, Prod(72), 195, true,  68),
            (18, Cust08, Prod(83), 225, true,  75),

            // Browse-only sessions (42 = 70%)
            (19, Cust09, Prod(2),  90,  false, 1),
            (20, Cust10, Prod(3),  65,  false, 1),
            (21, Cust01, Prod(4),  120, false, 2),
            (22, Cust02, Prod(5),  45,  false, 3),
            (23, Cust03, Prod(6),  110, false, 4),
            (24, Cust04, Prod(7),  75,  false, 4),
            (25, Cust05, Prod(8),  95,  false, 5),
            (26, Cust06, Prod(9),  55,  false, 6),
            (27, Cust07, Prod(10), 130, false, 7),
            (28, Cust08, Prod(11), 80,  false, 8),
            (29, Cust09, Prod(12), 100, false, 9),
            (30, Cust10, Prod(15), 60,  false, 10),
            (31, Cust01, Prod(16), 115, false, 11),
            (32, Cust02, Prod(17), 85,  false, 12),
            (33, Cust03, Prod(18), 105, false, 13),
            (34, Cust04, Prod(19), 70,  false, 14),
            (35, Cust05, Prod(20), 140, false, 16),
            (36, Cust06, Prod(21), 95,  false, 18),
            (37, Cust07, Prod(22), 60,  false, 20),
            (38, Cust08, Prod(24), 125, false, 22),
            (39, Cust09, Prod(27), 80,  false, 25),
            (40, Cust10, Prod(28), 110, false, 28),
            (41, Cust01, Prod(29), 55,  false, 30),
            (42, Cust02, Prod(30), 90,  false, 33),
            (43, Cust03, Prod(31), 135, false, 36),
            (44, Cust04, Prod(33), 75,  false, 40),
            (45, Cust05, Prod(34), 100, false, 43),
            (46, Cust06, Prod(35), 65,  false, 47),
            (47, Cust07, Prod(36), 120, false, 50),
            (48, Cust08, Prod(38), 85,  false, 54),
            (49, Cust09, Prod(39), 110, false, 58),
            (50, Cust10, Prod(40), 70,  false, 62),
            (51, Cust01, Prod(41), 95,  false, 65),
            (52, Cust02, Prod(43), 55,  false, 68),
            (53, Cust03, Prod(44), 130, false, 71),
            (54, Cust04, Prod(45), 80,  false, 74),
            (55, Cust05, Prod(46), 105, false, 77),
            (56, Cust06, Prod(48), 60,  false, 80),
            (57, Cust07, Prod(49), 115, false, 83),
            (58, Cust08, Prod(51), 85,  false, 85),
            (59, Cust09, Prod(52), 100, false, 87),
            (60, Cust10, Prod(53), 75,  false, 89),
        };

        foreach (var (idx, custId, prodId, durSec, purchased, daysAgo) in sessions)
        {
            var at = now.AddDays(-daysAgo);
            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO try_on_sessions
                    (id, retailer_id, product_id, customer_id,
                     session_duration_seconds, resulted_in_purchase, created_at)
                VALUES
                    ({Tryon(idx)}, {RetailerId}, {prodId}, {custId},
                     {durSec}, {purchased}, {at})
                ON CONFLICT (id) DO NOTHING
                """, ct);
        }

        _log.LogDebug("TransactionalDataSeeder: try-on sessions seeded.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 12. vfr_engagement_metrics (daily, last 30 days)
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SeedVfrEngagementMetricsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Pre-computed daily metrics matching the try_on_sessions seeded above
        // Each row = (daysAgo, totalTryOns, uniqueCustomers, avgSessionSec, convRate)
        var metrics = new[]
        {
            (1,  3, 3,  83.3m, 0.3333m, Prod(2)),
            (2,  2, 2, 152.5m, 0.5000m, Prod(1)),
            (3,  2, 2, 142.5m, 0.5000m, Prod(13)),
            (4,  3, 3,  91.7m, 0.3333m, Prod(6)),
            (5,  2, 2, 152.5m, 0.5000m, Prod(26)),
            (6,  2, 2,  75.0m, 0.0000m, Prod(9)),
            (7,  2, 2, 162.5m, 0.5000m, Prod(37)),
            (8,  2, 2,  92.5m, 0.0000m, Prod(11)),
            (9,  2, 2, 157.5m, 0.5000m, Prod(42)),
            (10, 2, 2,  80.0m, 0.0000m, Prod(15)),
            (11, 2, 2, 167.5m, 0.5000m, Prod(50)),
            (12, 2, 2,  97.5m, 0.0000m, Prod(17)),
            (13, 2, 2, 122.5m, 0.0000m, Prod(18)),
            (14, 3, 3, 149.7m, 0.3333m, Prod(56)),
            (16, 2, 2, 117.5m, 0.0000m, Prod(20)),
            (17, 1, 1, 175.0m, 1.0000m, Prod(65)),
            (18, 2, 2, 132.5m, 0.0000m, Prod(21)),
            (20, 2, 2, 147.5m, 0.5000m, Prod(75)),
            (22, 2, 2, 105.0m, 0.0000m, Prod(24)),
            (24, 1, 1, 215.0m, 1.0000m, Prod(86)),
            (25, 2, 2,  95.0m, 0.0000m, Prod(27)),
            (28, 3, 3, 148.3m, 0.3333m, Prod(97)),
            (30, 2, 2, 100.0m, 0.0000m, Prod(30)),
        };

        int seq = 1;
        foreach (var (daysAgo, total, unique, avgSec, conv, topProd) in metrics)
        {
            var metricDate = DateOnly.FromDateTime(now.AddDays(-daysAgo));
            var recordedAt = now.AddDays(-daysAgo + 1); // recorded next day

            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO vfr_engagement_metrics
                    (id, retailer_id, metric_date,
                     total_try_ons, unique_customers,
                     avg_session_seconds, conversion_rate,
                     top_product_id, recorded_at)
                VALUES
                    ({Metric(seq++)}, {RetailerId}, {metricDate},
                     {total}, {unique},
                     {avgSec}, {conv},
                     {topProd}, {recordedAt})
                ON CONFLICT (id) DO NOTHING
                """, ct);
        }

        _log.LogDebug("TransactionalDataSeeder: VFR engagement metrics seeded.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 13. dashboard_snapshots (daily KPIs, last 30 days)
    // ═════════════════════════════════════════════════════════════════════════

    private async Task SeedDashboardSnapshotsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Realistic KPI data — revenue grows week-over-week, low stock fluctuates
        // Format: (daysAgo, revenue, profit, orders, activeProds, lowStock, convRate, tryOnEng)
        var snapshots = new[]
        {
            (1,   489.98m,  195.99m, 1, 100, 8, 0.3333m, 3),
            (2,   829.96m,  331.98m, 2, 100, 8, 0.5000m, 2),
            (3,   639.97m,  255.99m, 1, 100, 7, 0.5000m, 2),
            (4,   559.97m,  223.99m, 1, 100, 7, 0.3333m, 3),
            (5,   369.98m,  147.99m, 1, 100, 7, 0.5000m, 2),
            (6,     0.00m,    0.00m, 0, 100, 6, 0.0000m, 2),
            (7,   489.98m,  195.99m, 1, 100, 6, 0.5000m, 2),
            (8,   249.99m,   99.99m, 1, 100, 7, 0.0000m, 2),
            (9,   769.97m,  307.99m, 2, 100, 7, 0.5000m, 2),
            (10,  549.98m,  219.99m, 1, 100, 8, 0.0000m, 2),
            (11,  899.97m,  359.99m, 2, 100, 8, 0.5000m, 2),
            (12,  379.99m,  151.99m, 1, 100, 7, 0.0000m, 2),
            (13,  639.97m,  255.99m, 1, 100, 7, 0.0000m, 2),
            (14,  999.97m,  399.99m, 2, 100, 9, 0.3333m, 3),
            (15,  459.98m,  183.99m, 1, 100, 9, 1.0000m, 1),
            (16,  679.97m,  271.99m, 1, 100, 8, 0.0000m, 2),
            (17,  829.96m,  331.98m, 2, 100, 8, 1.0000m, 1),
            (18,  549.98m,  219.99m, 1, 100, 7, 0.0000m, 2),
            (19,  389.99m,  155.99m, 1, 100, 7, 0.5000m, 2),
            (20, 1099.96m,  439.98m, 2, 100, 9, 0.5000m, 2),
            (21,  649.97m,  259.99m, 1, 100, 9, 0.0000m, 0),
            (22,  449.98m,  179.99m, 1, 100, 8, 0.0000m, 2),
            (23,    0.00m,    0.00m, 0, 100, 8, 0.0000m, 0),
            (24,  899.97m,  359.99m, 2, 100, 7, 1.0000m, 1),
            (25,  729.97m,  291.99m, 1, 100, 7, 0.0000m, 2),
            (26,  559.98m,  223.99m, 1, 100, 8, 0.0000m, 0),
            (27,  339.99m,  135.99m, 1, 100, 8, 0.0000m, 0),
            (28,  999.97m,  399.99m, 2, 100, 9, 0.3333m, 3),
            (29,  649.97m,  259.99m, 1, 100, 9, 0.0000m, 0),
            (30,  489.98m,  195.99m, 1, 100, 8, 0.0000m, 2),
        };

        int seq = 1;
        foreach (var (daysAgo, revenue, profit, orders, activeProds, lowStock, convRate, tryOnEng) in snapshots)
        {
            var snapDate = DateOnly.FromDateTime(now.AddDays(-daysAgo));
            var computedAt = now.AddDays(-daysAgo + 1).Date.AddHours(1); // 01:00 UTC nightly job

            await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO dashboard_snapshots
                    (id, retailer_id, snapshot_date,
                     total_revenue, total_profit, total_orders,
                     active_products, low_stock_count,
                     conversion_rate, try_on_engagement, computed_at)
                VALUES
                    ({Snap(seq++)}, {RetailerId}, {snapDate},
                     {revenue}, {profit}, {orders},
                     {activeProds}, {lowStock},
                     {convRate}, {tryOnEng}, {computedAt})
                ON CONFLICT (id) DO NOTHING
                """, ct);
        }

        _log.LogDebug("TransactionalDataSeeder: dashboard snapshots seeded.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Private helpers
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Maps a product GUID (cccccccc pattern) back to its inventory GUID (eeeeeeee pattern).
    /// Both patterns are deterministic from ExcelDataSeeder index n.
    /// </summary>
    private static Guid InventoryIdFromProductIndex(Guid productId)
    {
        // Extract the 8-digit suffix from the product GUID's last segment
        // e.g. cccccccc-cccc-cccc-cccc-cccc00000013 → index 13
        var str = productId.ToString();            // "cccccccc-cccc-cccc-cccc-cccc00000013"
        var last = str[^12..];                      // "cccc00000013"
        var numPart = last[4..];                     // "00000013"
        if (int.TryParse(numPart, out int n))
            return Inv(n);
        return Inv(1);
    }

    // ── Order data definitions ────────────────────────────────────────────────

    private sealed record OrderItemRow(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);

    private sealed record OrderRow(
        Guid Id,
        Guid CustomerId,
        string CustomerName,
        DateTime OrderDate,
        string Status,
        DateTime UpdatedAt,
        decimal TotalAmount,
        List<OrderItemRow> Items);

    /// <summary>
    /// Builds all 30 order seed rows in-memory so both SeedOrdersAndItemsAsync
    /// and SeedCommissionRecordsAsync operate on identical data.
    /// </summary>
    private static List<OrderRow> BuildOrderSeedRows(DateTime now)
    {
        List<OrderRow> orders = [];

        // ── Helper: new order ─────────────────────────────────────────────────
        OrderRow MakeOrder(
            int ordSeq,
            Guid custId,
            string custName,
            int daysAgo,
            string status,
            int resolvedDaysAfter,
            List<OrderItemRow> items)
        {
            var orderDate = now.AddDays(-daysAgo);
            var updatedAt = status == "NotProcessed"
                                 ? orderDate
                                 : orderDate.AddDays(resolvedDaysAfter);
            var total = items.Sum(i => i.UnitPrice * i.Quantity);
            return new OrderRow(Ord(ordSeq), custId, custName, orderDate, status, updatedAt, total, items);
        }

        // ── Orders 1–5 : Delivered (80–90 days ago) ───────────────────────────
        orders.Add(MakeOrder(1, Cust01, "Nour Ahmed Hassan", 90, "Delivered", 5, [
            new(Prod(1),  "Asymmetrical Wrap Black Blazer",           189.99m, 1),
            new(Prod(26), "Wide-Leg Trousers Off-White",              159.99m, 1),
        ]));
        orders.Add(MakeOrder(2, Cust02, "Laila Mohamed Omar", 86, "Delivered", 5, [
            new(Prod(13), "Sleek Black Leather Jacket",               289.99m, 1),
            new(Prod(32), "Classic Black Wide-Leg Formal Trousers",   169.99m, 1),
        ]));
        orders.Add(MakeOrder(3, Cust03, "Sara Ibrahim Khalil", 83, "Delivered", 6, [
            new(Prod(7),  "Nautical Quarter-Zip Breton Pullover",     149.99m, 1),
            new(Prod(50), "Black Accordion Pleated Midi Skirt",       189.99m, 1),
        ]));
        orders.Add(MakeOrder(4, Cust04, "Maryam Youssef Ali", 79, "Delivered", 5, [
            new(Prod(42), "Suede Cocoa Brown Box-Pleat Midi Skirt",   219.99m, 1),
            new(Prod(14), "Brown Faux-Leather Bomber-Moto Jacket",    259.99m, 1),
            new(Prod(2),  "Pinstripe Knotted Boat-Neck Blouse",       129.99m, 1),
        ]));
        orders.Add(MakeOrder(5, Cust05, "Hana Mahmoud Saad", 75, "Delivered", 6, [
            new(Prod(37), "Dark Indigo High-Waisted Wide-Leg Jeans",  219.99m, 1),
            new(Prod(23), "Burgundy Bell-Sleeve Ribbed Knit Top",     149.99m, 1),
        ]));

        // ── Orders 6–10 : Delivered (55–70 days ago) ──────────────────────────
        orders.Add(MakeOrder(6, Cust06, "Rania Khaled Farouk", 70, "Delivered", 7, [
            new(Prod(15), "Light Blue LE3NO Denim Trucker Jacket",    179.99m, 1),
            new(Prod(47), "Black Faux-Leather Pencil Midi Skirt",     199.99m, 1),
        ]));
        orders.Add(MakeOrder(7, Cust07, "Dina Tarek Mostafa", 65, "Delivered", 5, [
            new(Prod(4),  "Two-Tone Vintage Knit Cardigan Top",       169.99m, 1),
            new(Prod(28), "Burgundy High-Waisted Wide-Leg Trousers",  179.99m, 1),
        ]));
        orders.Add(MakeOrder(8, Cust08, "Yasmine Adel Nasser", 60, "Delivered", 6, [
            new(Prod(5),  "Elegant Two-Piece White Blazer Set",       249.99m, 1),
            new(Prod(41), "Chocolate Leather A-Line Mini Skirt",      239.99m, 1),
        ]));
        orders.Add(MakeOrder(9, Cust09, "Amira Hassan Zaki", 57, "Delivered", 5, [
            new(Prod(9),  "Navy Half-Zip Chunky Ribbed Pullover",     189.99m, 1),
            new(Prod(43), "Khaki Utility Maxi Skirt with Belt",       199.99m, 1),
        ]));
        orders.Add(MakeOrder(10, Cust10, "Mariam Samer Lotfy", 54, "Delivered", 5, [
            new(Prod(17), "Dusty Pink Leather Moto Jacket",           299.99m, 1),
            new(Prod(26), "Wide-Leg Trousers Off-White",              159.99m, 1),
            new(Prod(6),  "Cream Short-Sleeve Knit Cardigan Top",     149.99m, 1),
        ]));

        // ── Orders 11–12 : Delivered (30–45 days ago) ─────────────────────────
        orders.Add(MakeOrder(11, Cust01, "Nour Ahmed Hassan", 45, "Delivered", 6, [
            new(Prod(56), "Chocolate Brown Wrap Maxi Dress",          349.99m, 1),
            new(Prod(30), "Navy High-Waisted Wide-Leg Trousers",      189.99m, 1),
        ]));
        orders.Add(MakeOrder(12, Cust02, "Laila Mohamed Omar", 38, "Delivered", 7, [
            new(Prod(65), "Black Maxi Dress with Gold Buttons",       299.99m, 1),
            new(Prod(12), "Calvin Klein Striped Mock-Neck Sweater",   169.99m, 1),
        ]));

        // ── Orders 13–14 : Cancelled ──────────────────────────────────────────
        orders.Add(MakeOrder(13, Cust03, "Sara Ibrahim Khalil", 33, "Cancelled", 1, [
            new(Prod(17), "Dusty Pink Leather Moto Jacket",           299.99m, 1),
        ]));
        orders.Add(MakeOrder(14, Cust04, "Maryam Youssef Ali", 28, "Cancelled", 2, [
            new(Prod(5),  "Elegant Two-Piece White Blazer Set",       249.99m, 1),
        ]));

        // ── Orders 15–19 : Delivered (10–25 days ago) ─────────────────────────
        orders.Add(MakeOrder(15, Cust05, "Hana Mahmoud Saad", 25, "Delivered", 5, [
            new(Prod(74), "Terracotta Pleated Puff-Sleeve Maxi Dress",299.99m, 1),
            new(Prod(27), "Ivory High-Waisted Pleated Trousers",      159.99m, 1),
        ]));
        orders.Add(MakeOrder(16, Cust06, "Rania Khaled Farouk", 22, "Delivered", 4, [
            new(Prod(16), "Classic Black Denim Trucker Jacket",       179.99m, 1),
            new(Prod(23), "Burgundy Bell-Sleeve Ribbed Knit Top",     149.99m, 1),
        ]));
        orders.Add(MakeOrder(17, Cust07, "Dina Tarek Mostafa", 19, "Delivered", 5, [
            new(Prod(86), "Cream Oversized Ribbed Turtleneck Sweater",199.99m, 1),
            new(Prod(29), "Dusty Rose High-Waisted Wide-Leg Trousers",169.99m, 1),
        ]));
        orders.Add(MakeOrder(18, Cust08, "Yasmine Adel Nasser", 16, "Delivered", 4, [
            new(Prod(75), "Burgundy Tiered Pleated Maxi Dress",       319.99m, 1),
            new(Prod(18), "Pink Pinstripe Fitted Button-Down Shirt",   129.99m, 1),
        ]));
        orders.Add(MakeOrder(19, Cust09, "Amira Hassan Zaki", 13, "Delivered", 4, [
            new(Prod(85), "Black Bow-Closure Oversized Knit Cardigan",239.99m, 1),
            new(Prod(31), "Charcoal Espresso Wide-Leg Trousers",      159.99m, 1),
        ]));

        // ── Orders 20–24 : Shipped ────────────────────────────────────────────
        orders.Add(MakeOrder(20, Cust10, "Mariam Samer Lotfy", 11, "Shipped", 2, [
            new(Prod(97), "Brown Polka Dot V-Neck Button Maxi Dress", 289.99m, 1),
            new(Prod(10), "Navy Cable-Knit Polo-Collar Sweater",      149.99m, 1),
        ]));
        orders.Add(MakeOrder(21, Cust01, "Nour Ahmed Hassan", 10, "Shipped", 2, [
            new(Prod(8),  "Navy-Stripe Crewneck x Stripe Shacket",    179.99m, 1),
            new(Prod(44), "Ivory Off-White Denim Maxi Skirt",         199.99m, 1),
        ]));
        orders.Add(MakeOrder(22, Cust02, "Laila Mohamed Omar", 9, "Shipped", 2, [
            new(Prod(3),  "MICAS Mixed-Media Corset Statement Top",   159.99m, 1),
            new(Prod(49), "Burgundy Accordion Pleated Midi Skirt",    189.99m, 1),
        ]));
        orders.Add(MakeOrder(23, Cust03, "Sara Ibrahim Khalil", 8, "Shipped", 1, [
            new(Prod(11), "Dark Wash Denim Corset Shacket",           229.99m, 1),
            new(Prod(38), "Light Blue High-Waisted Wide-Leg Jeans",   199.99m, 1),
        ]));
        orders.Add(MakeOrder(24, Cust04, "Maryam Youssef Ali", 7, "Shipped", 2, [
            new(Prod(100),"Olive Green Maxi Skirt with Yoke Detailing",219.99m, 1),
            new(Prod(19), "White Princess-Seam Button-Down Shirt",     129.99m, 1),
        ]));

        // ── Orders 25–27 : Processing ─────────────────────────────────────────
        orders.Add(MakeOrder(25, Cust05, "Hana Mahmoud Saad", 5, "Processing", 1, [
            new(Prod(88), "Black Double-Breasted Knit Jacket",         259.99m, 1),
            new(Prod(34), "Beige Camel Faux-Leather Wide-Leg Pants",   219.99m, 1),
        ]));
        orders.Add(MakeOrder(26, Cust06, "Rania Khaled Farouk", 4, "Processing", 1, [
            new(Prod(54), "Oversized Classic White Button-Down Shirt",  139.99m, 1),
            new(Prod(45), "Medium Blue Denim Column Maxi Skirt",        199.99m, 1),
            new(Prod(36), "Black Criss-Cross Waistband Faux-Leather",   219.99m, 1),
        ]));
        orders.Add(MakeOrder(27, Cust07, "Dina Tarek Mostafa", 3, "Processing", 1, [
            new(Prod(95), "Black Lace-Up V-Neck Bell-Sleeve Blouse",   199.99m, 1),
            new(Prod(21), "Cream Asymmetrical Draped Knit Top",        149.99m, 1),
        ]));

        // ── Orders 28–30 : NotProcessed (newest) ─────────────────────────────
        orders.Add(MakeOrder(28, Cust08, "Yasmine Adel Nasser", 2, "NotProcessed", 0, [
            new(Prod(1),  "Asymmetrical Wrap Black Blazer",           189.99m, 1),
        ]));
        orders.Add(MakeOrder(29, Cust09, "Amira Hassan Zaki", 1, "NotProcessed", 0, [
            new(Prod(65), "Black Maxi Dress with Gold Buttons",        299.99m, 1),
            new(Prod(87), "Off-White Polo-Collar Knit Sweater",        189.99m, 1),
            new(Prod(22), "Light Blue Balloon-Sleeve Pinstripe Shirt", 149.99m, 1),
        ]));
        orders.Add(MakeOrder(30, Cust10, "Mariam Samer Lotfy", 0, "NotProcessed", 0, [
            new(Prod(13), "Sleek Black Leather Jacket",               289.99m, 1),
            new(Prod(50), "Black Accordion Pleated Midi Skirt",        189.99m, 1),
        ]));

        return orders;
    }
}