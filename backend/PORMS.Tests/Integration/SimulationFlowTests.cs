using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PORMS.API.Contracts;
using Xunit;

namespace PORMS.Tests.Integration;

[Collection(DatabaseBackedIntegrationCollection.Name)]
public sealed class SimulationFlowTests
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public SimulationFlowTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RunDemo_ReturnsNotFound_WhenPortDoesNotExist()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/simulation/run-demo",
            new SimulationRunRequest { PortCode = "UNKNOWN-PORT" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("The requested port was not found.", error!.Error);
    }

    [Fact]
    public async Task RunDemo_WritesDeterministicSimulationFlow()
    {
        await _factory.ResetPrimaryPortStateAsync();
        var port = await _factory.GetPrimaryPortAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/simulation/run-demo",
            new SimulationRunRequest { PortCode = port.PortCode });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var sessionId = payload.RootElement.GetProperty("sessionId").GetGuid();
        var finalRiskLevel = payload.RootElement.GetProperty("finalRiskLevel").GetString();
        var finalOperationMode = payload.RootElement.GetProperty("finalOperationMode").GetString();

        Assert.Equal("CRITICAL", finalRiskLevel);
        Assert.Equal("STOP", finalOperationMode);

        var snapshot = await _factory.GetSimulationSessionSnapshotAsync(sessionId);

        Assert.Equal(port.PortId, snapshot.PortId);
        Assert.Equal("COMPLETED", snapshot.Status);
        Assert.Equal(100m, snapshot.ProgressPercent);
        Assert.Equal(4, snapshot.CurrentSnapshotNumber);
        Assert.Equal("CRITICAL", snapshot.PeakRiskLevel);
        Assert.Equal(2, snapshot.GeneratedAlertCount);
        Assert.Equal(2, snapshot.ModeChangeCount);
        Assert.Equal("CRITICAL", snapshot.CurrentRiskLevel);
        Assert.Equal("STOP", snapshot.CurrentOperationMode);
        Assert.Equal(4, snapshot.WeatherReadingCount);
        Assert.Equal(4, snapshot.RiskAssessmentCount);
        Assert.Equal(2, snapshot.AlertCount);
        Assert.Equal(2, snapshot.ModeLogCount);
        Assert.Equal(6, snapshot.OperationEventCount);

        var simulationResponse = await client.GetAsync("/api/simulation/current");
        Assert.Equal(HttpStatusCode.OK, simulationResponse.StatusCode);

        using var simulationPayload = JsonDocument.Parse(await simulationResponse.Content.ReadAsStringAsync());
        Assert.Equal(27.4m, simulationPayload.RootElement.GetProperty("windSpeedMs").GetDecimal());
        Assert.Equal(60m, simulationPayload.RootElement.GetProperty("rainfall1hMm").GetDecimal());
        Assert.Equal(0.8m, simulationPayload.RootElement.GetProperty("visibilityKm").GetDecimal());

        var feed = simulationPayload.RootElement.GetProperty("feed").EnumerateArray().ToList();
        Assert.Contains(feed, item => item.GetProperty("riskLevel").GetString() == "CRITICAL");
    }

    [Fact]
    public async Task RunDataset_ResultUsesLatestZoneRisk()
    {
        var port = await _factory.GetPrimaryPortAsync();
        var zone = await _factory.GetFirstZoneAsync(port.PortId);
        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/datasets",
            new CreateSimulationDatasetRequest
            {
                Name = $"Latest zone risk {Guid.NewGuid():N}",
                Description = "Critical first, then low for the same zone.",
                PortCode = port.PortCode,
                Snapshots =
                [
                    new CreateSimulationSnapshotRequest
                    {
                        SnapshotNumber = 1,
                        WindSpeedMs = 27.4m,
                        BeaufortNumber = 10,
                        Rainfall1hMm = 60m,
                        VisibilityKm = 0.8m,
                        ZoneId = zone.ZoneId
                    },
                    new CreateSimulationSnapshotRequest
                    {
                        SnapshotNumber = 2,
                        WindSpeedMs = 4.5m,
                        BeaufortNumber = 3,
                        Rainfall1hMm = 1m,
                        VisibilityKm = 14m,
                        ZoneId = zone.ZoneId
                    }
                ]
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<SimulationDatasetSummaryResponse>();
        Assert.NotNull(created);

        var runResponse = await client.PostAsJsonAsync(
            "/api/simulation/run",
            new RunSimulationDatasetRequest { DatasetId = created!.DatasetId });

        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);

        using var runPayload = JsonDocument.Parse(await runResponse.Content.ReadAsStringAsync());
        var sessionId = runPayload.RootElement.GetProperty("sessionId").GetGuid();

        var resultResponse = await client.GetAsync($"/api/simulation/{sessionId}/result");
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);

        using var resultPayload = JsonDocument.Parse(await resultResponse.Content.ReadAsStringAsync());
        var mapPoint = resultPayload.RootElement.GetProperty("mapPoints")
            .EnumerateArray()
            .Single(item => item.GetProperty("zoneId").GetGuid() == zone.ZoneId);

        Assert.Equal("LOW", mapPoint.GetProperty("riskLevel").GetString());
        Assert.DoesNotContain(
            resultPayload.RootElement.GetProperty("dangerousZones").EnumerateArray(),
            item => item.GetProperty("zoneId").GetGuid() == zone.ZoneId);
        Assert.DoesNotContain(
            resultPayload.RootElement.GetProperty("tasks").EnumerateArray(),
            item => item.GetProperty("zoneName").GetString() == zone.ZoneName);
    }

    [Fact]
    public async Task RunDataset_CreatesAlertWithCurrentTimeAndZoneInformation()
    {
        var port = await _factory.GetPrimaryPortAsync();
        var zone = await _factory.GetFirstZoneAsync(port.PortId);
        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/datasets",
            new CreateSimulationDatasetRequest
            {
                Name = $"Alert detail {Guid.NewGuid():N}",
                Description = "High risk alert for a specific zone.",
                PortCode = port.PortCode,
                Snapshots =
                [
                    new CreateSimulationSnapshotRequest
                    {
                        SnapshotNumber = 1,
                        WindSpeedMs = 18.2m,
                        BeaufortNumber = 8,
                        Rainfall1hMm = 28m,
                        VisibilityKm = 4m,
                        ZoneId = zone.ZoneId
                    }
                ]
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<SimulationDatasetSummaryResponse>();
        Assert.NotNull(created);

        var startedAt = DateTimeOffset.UtcNow;
        var runResponse = await client.PostAsJsonAsync(
            "/api/simulation/run",
            new RunSimulationDatasetRequest { DatasetId = created!.DatasetId });
        var completedAt = DateTimeOffset.UtcNow;

        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);

        using var runPayload = JsonDocument.Parse(await runResponse.Content.ReadAsStringAsync());
        var sessionId = runPayload.RootElement.GetProperty("sessionId").GetGuid();
        var alert = await _factory.GetLatestAlertForSessionAsync(sessionId);

        Assert.Equal(zone.ZoneId, alert.ZoneId);
        Assert.Contains(zone.ZoneName, alert.Title);
        Assert.Contains("HIGH", alert.Title);
        Assert.Contains(zone.ZoneName, alert.Message);
        Assert.DoesNotContain("Port conditions reached", alert.Message);
        Assert.True(alert.CreatedAt >= startedAt.AddSeconds(-2), $"Alert created_at {alert.CreatedAt:o} was before run start {startedAt:o}.");
        Assert.True(alert.CreatedAt <= completedAt.AddSeconds(2), $"Alert created_at {alert.CreatedAt:o} was after run completed {completedAt:o}.");
    }

    [Fact]
    public async Task CreateForecastPlan_BuildsFuturePlanningDatasetFromLatestWeather()
    {
        await _factory.SeedForecastWeatherAsync(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee1"));
        var port = await _factory.GetPrimaryPortAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/simulation/forecast-plan",
            new CreateForecastPlanRequest
            {
                PortCode = port.PortCode,
                HorizonDays = 5
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plan = await response.Content.ReadFromJsonAsync<ForecastPlanResponse>();
        Assert.NotNull(plan);
        Assert.Equal(5, plan!.HorizonDays);
        Assert.Equal(5, plan.Items.Count);
        Assert.Equal(5, plan.Dataset.SnapshotCount);
        Assert.Equal(port.PortCode, plan.Dataset.PortCode);
        Assert.NotEqual(Guid.Empty, plan.Dataset.DatasetId);
        Assert.All(plan.Items, item =>
        {
            Assert.True(item.PlannedAt > DateTimeOffset.UtcNow.AddHours(-1));
            Assert.False(string.IsNullOrWhiteSpace(item.OperationPlan));
            Assert.Contains(item.RiskLevel, new[] { "LOW", "MEDIUM", "HIGH", "CRITICAL" });
        });
    }
}
