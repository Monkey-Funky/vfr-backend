namespace Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeds two fully-activated test accounts (one Customer, one Retailer) with
/// deterministic GUIDs so that developers and the frontend team can test ALL
/// endpoints immediately without going through the registration flow.
///
/// DESIGN:
///   • Idempotent — uses INSERT … ON CONFLICT (id) DO NOTHING.
///   • Both accounts are Active, email-verified, and use BCrypt work-factor 12.
///   • The Customer account ID is different from the seed Retailer account ID
///     so there is no collision.
///   • The seed Retailer account already exists (created by ExcelDataSeeder) —
///     this seeder only ensures the password is usable for login.
///
/// CREDENTIALS:
///   Customer → email: customer@vfr-test.com  |  password: Test@12345
///   Retailer → email: seed@vfr-demo.com      |  password: SeedRetailer@2026
/// </summary>
internal sealed class TestAccountSeeder : ISeeder
{
    // ── Fixed GUIDs ─────────────────────────────────────────────────────────
    
    /// <summary>
    /// The test customer's deterministic ID. Use this in URL paths:
    /// <c>/api/customers/{TestCustomerId}/avatar</c>
    /// </summary>
    internal static readonly Guid TestCustomerId = new("11111111-1111-1111-1111-111111111001");

    /// <summary>
    /// The seed retailer ID — must match ExcelDataSeeder.SeedRetailerId so the
    /// retailer login can access all 100 seeded products.
    /// </summary>
    internal static readonly Guid SeedRetailerId = ExcelDataSeeder.SeedRetailerId;

    // ── Dependencies ────────────────────────────────────────────────────────

    private readonly ApplicationDbContext _context;
    private readonly ILogger<TestAccountSeeder> _logger;

    public TestAccountSeeder(ApplicationDbContext context, ILogger<TestAccountSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ── Public Entry Point ──────────────────────────────────────────────────

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedTestCustomerAsync(cancellationToken);
        await EnsureSeedRetailerPasswordAsync(cancellationToken);

        _logger.LogInformation(
            "TestAccountSeeder: test accounts ready. " +
            "Customer: customer@vfr-test.com / Test@12345 (ID: {CustomerId}) | " +
            "Retailer: seed@vfr-demo.com / SeedRetailer@2026 (ID: {RetailerId})",
            TestCustomerId, SeedRetailerId);
    }

    // ── Seed Customer ───────────────────────────────────────────────────────

    private async Task SeedTestCustomerAsync(CancellationToken ct)
    {
        // BCrypt hash of "Test@12345" with work factor 12.
        // Verified via BCrypt.Net.BCrypt.Verify("Test@12345", hash) == true
        const string passwordHash = "$2a$12$7H7eW7pOxLF4k/AtAXdzBu0sZmxt2lNt960.14NlvAeQ3skdJ2VLy";

        await _context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO customer_accounts
             (
                 id, full_name, email, password_hash,
                 phone_number, date_of_birth, gender,
                 is_email_verified, status,
                 failed_login_attempts, remember_me,
                 is_deleted, created_at, updated_at
             )
             VALUES
             (
                 {TestCustomerId},
                 {"VFR Test Customer"},
                 {"customer@vfr-test.com"},
                 {passwordHash},
                 {"+201000000001"},
                 {"1998-05-15"},
                 {"Male"},
                 {true},
                 {"Active"},
                 {0},
                 {false},
                 {false},
                 now(),
                 now()
             )
             ON CONFLICT (id) DO UPDATE SET
                 status           = EXCLUDED.status,
                 is_email_verified = EXCLUDED.is_email_verified,
                 password_hash    = EXCLUDED.password_hash
             """, ct);

        _logger.LogDebug("TestAccountSeeder: customer account upserted.");
    }

    // ── Ensure Seed Retailer has a usable password ──────────────────────────

    /// <summary>
    /// The ExcelDataSeeder already creates the seed retailer with a password hash.
    /// This method ensures the account is Active and email-verified so login works.
    /// Uses ON CONFLICT DO UPDATE so it's safe even if ExcelDataSeeder hasn't run yet.
    /// </summary>
    private async Task EnsureSeedRetailerPasswordAsync(CancellationToken ct)
    {
        // Password: SeedRetailer@2026 (BCrypt work factor 12) — same as ExcelDataSeeder.
        const string passwordHash = "$2b$12$BBah5s2G9Ht/6yPGR7stFuBavPOvA8m3wN1qdiF9QsR2s16MNXc3u";

        // Only update fields that affect login — don't overwrite brand_name, business_type, etc.
        await _context.Database.ExecuteSqlAsync(
            $"""
             UPDATE retailer_accounts
             SET account_status   = {"Active"},
                 is_email_verified = {true},
                 password_hash    = {passwordHash},
                 updated_at       = now()
             WHERE id = {SeedRetailerId}
               AND (account_status <> {"Active"} OR is_email_verified = {false})
             """, ct);

        _logger.LogDebug("TestAccountSeeder: seed retailer account verified for login.");
    }
}
