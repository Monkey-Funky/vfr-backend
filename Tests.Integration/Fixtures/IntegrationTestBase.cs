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
    /// Resets the database to a clean state before each test method.
    /// </summary>
    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
