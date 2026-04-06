
namespace Infrastructure.Persistence.Seeders;

/// <summary>
/// Contract that every database seeder must implement.
///
/// Replaces the <c>(dynamic)seeder</c> hack in <see cref="DatabaseSeeder"/>.
/// Any seeder that does not implement this interface will produce a compile-time
/// error rather than a runtime RuntimeBinderException, which is far easier to diagnose.
///
/// Rules:
///   • Implementations must be idempotent — safe to call on every startup.
///   • Implementations must be registered in Infrastructure DependencyInjection.cs
///     with <c>services.AddScoped&lt;TSeeder&gt;()</c>.
/// </summary>
internal interface ISeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}