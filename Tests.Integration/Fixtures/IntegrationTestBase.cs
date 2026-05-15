using Domain.Common;
using Domain.Entities.Customer;
using Domain.Entities.Retailer;

namespace Tests.Integration.Fixtures;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected HttpClient? _customerClient;

    /// <summary>
    /// The plain-text password whose BCrypt hash is stored for the test retailer and customer.
    /// Used by auth tests that need to attempt login with the correct or wrong password.
    /// BCrypt.Net.BCrypt.HashPassword("TestPassword123!") pre-computed with workFactor=10.
    /// </summary>
    protected const string DefaultPasswordHash =
        "$2b$10$dh4veLVxuA/HItuei5rviOEpdZC.VXZtBk.6yqnccO4E8omRtTPYy";

    protected HttpClient CustomerClient =>
        _customerClient ??= Factory.CreateAuthenticatedClient(
            TestAuthHandler.DefaultCustomerId,
            "Customer",
            "test@customer.com");

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var retailer = RetailerAccount.Create(
                "Test Retailer",
                TestAuthHandler.DefaultEmail,
                DefaultPasswordHash,
                "TestBrand");

            var idProperty = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            idProperty!.SetValue(retailer, Guid.Parse(TestAuthHandler.DefaultRetailerId));

            retailer.CompleteRegistration("Fashion", false, null);
            db.RetailerAccounts.Add(retailer);

            var customer = CustomerAccount.Create(
                "Test Customer",
                "test@customer.com",
                DefaultPasswordHash);

            idProperty!.SetValue(customer, Guid.Parse(TestAuthHandler.DefaultCustomerId));
            customer.MarkEmailVerified();
            db.CustomerAccounts.Add(customer);

            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;
}