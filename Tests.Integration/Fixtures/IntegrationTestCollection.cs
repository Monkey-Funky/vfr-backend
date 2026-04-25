namespace Tests.Integration.Fixtures;

/// <summary>
/// xUnit collection definition that shares a single <see cref="CustomWebApplicationFactory"/>
/// (and therefore a single PostgreSQL container) across all tests in this collection.
///
/// Usage: decorate test classes with [Collection(IntegrationTestCollection.Name)].
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name = "Integration";
}
