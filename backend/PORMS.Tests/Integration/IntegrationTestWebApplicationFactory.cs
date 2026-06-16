using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace PORMS.Tests.Integration;

public sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestDatabaseConnectionEnvironmentVariable = "PORMS_TEST_DB_CONNECTION";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var connectionString = Environment.GetEnvironmentVariable(
                TestDatabaseConnectionEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Missing required environment variable '{TestDatabaseConnectionEnvironmentVariable}'.");
            }

            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Database:ConnectionString"] = connectionString
            });
        });
    }
}
