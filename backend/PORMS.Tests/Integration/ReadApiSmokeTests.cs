using System.Net;
using System.Net.Http.Json;
using PORMS.API.Contracts;
using Xunit;

namespace PORMS.Tests.Integration;

[Collection(DatabaseBackedIntegrationCollection.Name)]
public sealed class ReadApiSmokeTests
{
    private static readonly Guid SeedAlertId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SeedOperationEventId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly IntegrationTestWebApplicationFactory _factory;

    public ReadApiSmokeTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ports_ReturnSuccessAndContainSeededPort()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/ports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ports = await response.Content.ReadFromJsonAsync<List<PortSummaryResponse>>();
        Assert.NotNull(ports);
        var port = Assert.Single(ports!.Where(item => item.PortCode == "DNTSA"));
        Assert.Equal("DNTSA", port.PortCode);
        Assert.NotEqual(Guid.Empty, port.PortId);
        Assert.InRange(port.Latitude, -90m, 90m);
        Assert.InRange(port.Longitude, -180m, 180m);
    }

    [Fact]
    public async Task PortZones_ReturnSuccessAndContainSeededZones()
    {
        var client = _factory.CreateClient();
        var port = await _factory.GetPrimaryPortAsync();

        var response = await client.GetAsync($"/api/ports/{port.PortId}/zones");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var zones = await response.Content.ReadFromJsonAsync<List<ZoneResponse>>();
        Assert.NotNull(zones);
        Assert.NotEmpty(zones!);
        Assert.All(zones!, zone => Assert.Equal(port.PortId, zone.PortId));
        Assert.Contains(zones!, zone => zone.ZoneType is "DOCK" or "YARD" or "GATE" or "WAREHOUSE");
    }

    [Fact]
    public async Task Alerts_ReturnSuccessAndContainSeededAlert()
    {
        await _factory.SeedAlertAsync(SeedAlertId);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/alerts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var alerts = await response.Content.ReadFromJsonAsync<List<AlertResponse>>();
        Assert.NotNull(alerts);
        var alert = Assert.Single(alerts!.Where(item => item.AlertId == SeedAlertId));
        Assert.Equal("SYSTEM", alert.AlertType);
        Assert.Equal("HIGH", alert.Severity);
        Assert.Equal("Seeded smoke alert", alert.Title);
    }

    [Fact]
    public async Task OperationEvents_ReturnSuccessAndContainSeededOperationEvent()
    {
        await _factory.SeedOperationEventAsync(SeedOperationEventId);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/operation-events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await response.Content.ReadFromJsonAsync<List<OperationEventResponse>>();
        Assert.NotNull(events);
        var operationEvent = Assert.Single(events!.Where(item => item.OperationEventId == SeedOperationEventId));
        Assert.Equal("SYSTEM_TEST", operationEvent.EventType);
        Assert.Equal("Smoke test event", operationEvent.Summary);
        Assert.NotNull(operationEvent.PortCode);
    }
}
