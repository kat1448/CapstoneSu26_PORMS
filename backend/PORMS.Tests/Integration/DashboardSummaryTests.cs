using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace PORMS.Tests.Integration;

[Collection(DatabaseBackedIntegrationCollection.Name)]
public sealed class DashboardSummaryTests
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public DashboardSummaryTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DashboardSummary_ReturnsSuccess()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DashboardSummary_ReturnsNotFound_WhenNoPortStateExists()
    {
        await _factory.HideAllPortsAsync();

        try
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/dashboard/summary");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            var payload = await response.Content.ReadFromJsonAsync<ErrorPayload>();
            Assert.NotNull(payload);
            Assert.Equal("No dashboard data available.", payload!.Error);
        }
        finally
        {
            await _factory.RestoreAllPortsAsync();
        }
    }

    private sealed class ErrorPayload
    {
        public string Error { get; set; } = string.Empty;
    }
}
