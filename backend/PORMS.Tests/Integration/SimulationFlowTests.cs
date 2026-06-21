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

        var weatherResponse = await client.GetAsync("/api/weather/current");
        Assert.Equal(HttpStatusCode.OK, weatherResponse.StatusCode);

        using var weatherPayload = JsonDocument.Parse(await weatherResponse.Content.ReadAsStringAsync());
        Assert.Equal(27.4m, weatherPayload.RootElement.GetProperty("windSpeedMs").GetDecimal());
        Assert.Equal(60m, weatherPayload.RootElement.GetProperty("rainfall1hMm").GetDecimal());
        Assert.Equal(0.8m, weatherPayload.RootElement.GetProperty("visibilityKm").GetDecimal());

        var simulationResponse = await client.GetAsync("/api/simulation/current");
        Assert.Equal(HttpStatusCode.OK, simulationResponse.StatusCode);

        using var simulationPayload = JsonDocument.Parse(await simulationResponse.Content.ReadAsStringAsync());
        var feed = simulationPayload.RootElement.GetProperty("feed").EnumerateArray().ToList();
        Assert.Contains(feed, item => item.GetProperty("riskLevel").GetString() == "CRITICAL");
    }
}
