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
            configurationBuilder.SetBasePath(AppContext.BaseDirectory);
            configurationBuilder.AddJsonFile("appsettings.Testing.json", optional: false);

            var connectionString = Environment.GetEnvironmentVariable(
                TestDatabaseConnectionEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Database:ConnectionString"] = connectionString
            });
        });
    }
}
