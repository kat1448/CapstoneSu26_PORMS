using Xunit;

namespace PORMS.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatabaseBackedIntegrationCollection : ICollectionFixture<IntegrationTestWebApplicationFactory>
{
    public const string Name = "Database-backed integration";
}
