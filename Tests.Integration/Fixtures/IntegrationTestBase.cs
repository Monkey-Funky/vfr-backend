using Domain.Common;
using Domain.Entities.Retailer;

namespace Tests.Integration.Fixtures;

/// <summary>
/// Base class for integration tests that need an authenticated HTTP client
/// and database access. Resets the database before each test.
///
/// Usage:
///   [Collection(IntegrationTestCollection.Name)]
///   public class MyTests : IntegrationTestBase { ... }
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateAuthenticatedClient();
    }

    /// <summary>
    /// Resets the database to a clean state before each test method
    /// and seeds the default RetailerAccount required for JWT bypass.
    /// </summary>
    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var retailer = RetailerAccount.Create(
                "Test Retailer",
                TestAuthHandler.DefaultEmail,
                "fake-hash",
                "TestBrand");

            // Force ID to match TestAuthHandler.DefaultRetailerId for JWT compatibility
            var idProperty = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            idProperty!.SetValue(retailer, Guid.Parse(TestAuthHandler.DefaultRetailerId));

            // Move to Active status
            retailer.CompleteRegistration("Fashion", false, null);

            db.RetailerAccounts.Add(retailer);
            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
