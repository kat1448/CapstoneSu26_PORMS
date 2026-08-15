using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;
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

    [Fact]
    public async Task DashboardSummary_UsesLatestNonSimulationRiskAssessment()
    {
        await _factory.SeedDashboardRiskIsolationAsync();

        try
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/dashboard/summary");

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>();
            Assert.NotNull(payload);
            Assert.Equal("LOW", payload!.CurrentRiskLevel);
            Assert.Equal((short?)2, payload.BeaufortNumber);
            Assert.Equal((decimal?)0, payload.Rainfall1hMm);
            Assert.Equal((decimal?)10, payload.VisibilityKm);
        }
        finally
        {
            await _factory.CleanupDashboardRiskIsolationAsync();
        }
    }

    [Fact]
    public async Task AlertWithoutExpiration_GetsTwoHourDatabaseExpiration()
    {
        var port = await _factory.GetPrimaryPortAsync();
        var seeded = await _factory.SeedAlertWithoutExpirationAsync(port.PortId);

        try
        {
            Assert.Equal(TimeSpan.FromHours(2), seeded.ExpiresAt - seeded.CreatedAt);
        }
        finally
        {
            await _factory.DeleteAlertsAsync([seeded.AlertId]);
        }
    }

    [Fact]
    public async Task DashboardAndPortState_CountOnlyUnexpiredAlerts()
    {
        var client = _factory.CreateClient();
        var beforeResponse = await client.GetAsync("/api/dashboard/summary");
        beforeResponse.EnsureSuccessStatusCode();
        var beforeDashboard = await beforeResponse.Content.ReadFromJsonAsync<DashboardSummaryResponse>();
        Assert.NotNull(beforeDashboard);

        using var scope = _factory.Services.CreateScope();
        var portRepository = scope.ServiceProvider.GetRequiredService<PortRepository>();
        var beforePort = await portRepository.GetPortAsync(
            beforeDashboard!.PortId,
            CancellationToken.None);
        Assert.NotNull(beforePort);

        var seeded = await _factory.SeedAlertCountCasesAsync(beforeDashboard.PortId);

        try
        {
            var afterResponse = await client.GetAsync("/api/dashboard/summary");
            afterResponse.EnsureSuccessStatusCode();
            var afterDashboard = await afterResponse.Content.ReadFromJsonAsync<DashboardSummaryResponse>();
            Assert.NotNull(afterDashboard);

            var afterPort = await portRepository.GetPortAsync(
                beforeDashboard.PortId,
                CancellationToken.None);
            Assert.NotNull(afterPort);

            Assert.Equal(beforeDashboard.ActiveAlertCount + 1, afterDashboard!.ActiveAlertCount);
            Assert.Equal(beforePort!.ActiveAlertCount + 1, afterPort!.ActiveAlertCount);
        }
        finally
        {
            await _factory.DeleteAlertsAsync([seeded.ActiveAlertId, seeded.ExpiredAlertId]);
        }
    }

    private sealed class ErrorPayload
    {
        public string Error { get; set; } = string.Empty;
    }
}
