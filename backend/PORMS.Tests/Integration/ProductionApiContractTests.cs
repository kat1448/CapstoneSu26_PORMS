using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
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
        AuthorizeAsAdmin(client);

        var response = await client.GetAsync("/api/alerts");
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = payload.RootElement.EnumerateArray().First();

        Assert.True(first.TryGetProperty("read", out _));
    }

    [Fact]
    public async Task OperationEvents_ReturnFrontendReadModelFields()
    {
        await _factory.SeedOperationEventAsync(Guid.NewGuid());
        var client = _factory.CreateClient();
        AuthorizeAsAdmin(client);

        var response = await client.GetAsync("/api/operation-events");
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = payload.RootElement.EnumerateArray().First();

        Assert.True(first.TryGetProperty("tone", out _));
    }

    private static void AuthorizeAsAdmin(HttpClient client)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("PORMS-development-signing-key-change-in-production-2026"));
        var token = new JwtSecurityToken(
            issuer: "PORMS",
            audience: "PORMS.Frontend",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, "admin@porms.vn"),
                new Claim(ClaimTypes.Name, "ADMIN"),
                new Claim(ClaimTypes.Role, "ADMIN"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
    }

    [Theory]
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
