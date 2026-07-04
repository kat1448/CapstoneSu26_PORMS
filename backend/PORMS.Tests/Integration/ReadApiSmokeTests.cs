using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PORMS.API.Contracts;
using Xunit;

namespace PORMS.Tests.Integration;

[Collection(DatabaseBackedIntegrationCollection.Name)]
public sealed class ReadApiSmokeTests
{
    private static readonly Guid SeedAlertId = Guid.Parse("11111111-1111-1111-1111-111111111111");
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
    public async Task PortById_ReturnSuccessAndContainSeededPort()
    {
        var client = _factory.CreateClient();
        var port = await _factory.GetPrimaryPortAsync();

        var response = await client.GetAsync($"/api/ports/{port.PortId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<PortSummaryResponse>();
        Assert.NotNull(detail);
        Assert.Equal(port.PortId, detail!.PortId);
        Assert.Equal(port.PortCode, detail.PortCode);
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
    public async Task UpdatePort_ReturnsSuccessAndUpdatesPort()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("SUPER_ADMIN"));
        var port = await _factory.GetPrimaryPortAsync();

        var request = new UpdatePortRequest(
            port.PortCode,
            "Updated port name",
            "Updated address",
            16.1228m,
            108.2144m,
            "Asia/Ho_Chi_Minh",
            "OPENWEATHER",
            null,
            true);

        var response = await client.PutAsJsonAsync($"/api/ports/{port.PortId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<PortSummaryResponse>();
        Assert.NotNull(updated);
        Assert.Equal(port.PortId, updated!.PortId);
        Assert.Equal(port.PortCode, updated.PortCode);
        Assert.Equal("Updated port name", updated.PortName);
    }

    private static string CreateToken(string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("PORMS-development-signing-key-change-in-production-2026"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, $"{role.ToLowerInvariant()}@porms.vn"),
            new Claim(ClaimTypes.Name, role),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "PORMS",
            audience: "PORMS.Frontend",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
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
        var seedOperationEventId = Guid.NewGuid();
        await _factory.SeedOperationEventAsync(seedOperationEventId);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/operation-events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await response.Content.ReadFromJsonAsync<List<OperationEventResponse>>();
        Assert.NotNull(events);
        var operationEvent = Assert.Single(events!.Where(item => item.OperationEventId == seedOperationEventId));
        Assert.Equal("SYSTEM_TEST", operationEvent.EventType);
        Assert.Equal("Smoke test event", operationEvent.Summary);
        Assert.NotNull(operationEvent.PortCode);
    }

    [Fact]
    public async Task OperationEvents_SeparatesLiveAndSimulationLogs()
    {
        var liveEventId = Guid.NewGuid();
        var simulationEventId = Guid.NewGuid();
        var simulationSessionId = Guid.NewGuid();
        await _factory.SeedOperationEventAsync(liveEventId);
        await _factory.SeedSimulationOperationEventAsync(simulationEventId, simulationSessionId);
        var client = _factory.CreateClient();

        var liveResponse = await client.GetAsync("/api/operation-events");
        var simulationResponse = await client.GetAsync("/api/operation-events?scope=simulation");

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, simulationResponse.StatusCode);

        var liveEvents = await liveResponse.Content.ReadFromJsonAsync<List<OperationEventResponse>>();
        var simulationEvents = await simulationResponse.Content.ReadFromJsonAsync<List<OperationEventResponse>>();

        Assert.NotNull(liveEvents);
        Assert.NotNull(simulationEvents);
        Assert.Contains(liveEvents!, item => item.OperationEventId == liveEventId);
        Assert.DoesNotContain(liveEvents!, item => item.OperationEventId == simulationEventId);
        var simulationEvent = Assert.Single(simulationEvents!, item => item.OperationEventId == simulationEventId);
        Assert.True(simulationEvent.IsSimulation);
        Assert.Equal(simulationSessionId, simulationEvent.SimulationSessionId);
    }
}
