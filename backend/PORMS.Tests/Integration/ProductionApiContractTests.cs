using System.Text.Json;
using Xunit;

namespace PORMS.Tests.Integration;

[Collection(DatabaseBackedIntegrationCollection.Name)]
public sealed class ProductionApiContractTests
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public ProductionApiContractTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ports_ReturnFrontendReadModelFields()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/ports");
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = payload.RootElement.EnumerateArray().First();

        Assert.True(first.TryGetProperty("updatedAtLabel", out _));
    }

    [Fact]
    public async Task Zones_ReturnFrontendReadModelFields()
    {
        var port = await _factory.GetPrimaryPortAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/ports/{port.PortId}/zones");
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = payload.RootElement.EnumerateArray().First();

        Assert.True(first.TryGetProperty("capacityLabel", out _));
        Assert.True(first.TryGetProperty("statusLabel", out _));
        Assert.True(first.TryGetProperty("overrideEnabled", out _));
    }

    [Fact]
    public async Task Alerts_ReturnFrontendReadModelFields()
    {
        await _factory.SeedAlertAsync(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/alerts");
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = payload.RootElement.EnumerateArray().First();

        Assert.True(first.TryGetProperty("read", out _));
    }

    [Fact]
    public async Task OperationEvents_ReturnFrontendReadModelFields()
    {
        await _factory.SeedOperationEventAsync(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/operation-events");
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = payload.RootElement.EnumerateArray().First();

        Assert.True(first.TryGetProperty("tone", out _));
    }

    [Theory]
    [InlineData("/api/users")]
    [InlineData("/api/weather/current")]
    [InlineData("/api/risk/trend")]
    [InlineData("/api/simulation/current")]
    public async Task MissingFrontendEndpoints_ReturnSuccess(string path)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task WeatherCurrent_ReturnsFrontendFields()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/weather/current");
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.True(payload.RootElement.TryGetProperty("windSpeedMs", out _));
        Assert.True(payload.RootElement.TryGetProperty("rainfall1hMm", out _));
        Assert.True(payload.RootElement.TryGetProperty("visibilityKm", out _));
        Assert.True(payload.RootElement.TryGetProperty("humidityPct", out _));
        Assert.True(payload.RootElement.TryGetProperty("temperatureC", out _));
    }

    [Fact]
    public async Task SimulationCurrent_ReturnsFrontendFields()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/simulation/current");
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.True(payload.RootElement.TryGetProperty("status", out _));
        Assert.True(payload.RootElement.TryGetProperty("currentRiskLevel", out _));
        Assert.True(payload.RootElement.TryGetProperty("currentMode", out _));
        Assert.True(payload.RootElement.TryGetProperty("feed", out _));
    }
}
