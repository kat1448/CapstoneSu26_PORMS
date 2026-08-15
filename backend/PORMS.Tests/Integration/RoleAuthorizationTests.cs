using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace PORMS.Tests.Integration;

[Collection(DatabaseBackedIntegrationCollection.Name)]
public sealed class RoleAuthorizationTests
{
    private readonly IntegrationTestWebApplicationFactory _factory;

    public RoleAuthorizationTests(IntegrationTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Operator_CannotManageUsers()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("OPERATOR"));

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            email = "",
            fullName = "",
            password = "",
            role = "OPERATOR",
            status = "ACTIVE",
            portId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PortManager_CannotManageUsers()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("PORT_MANAGER"));

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            email = "",
            fullName = "",
            password = "",
            role = "PORT_MANAGER",
            status = "ACTIVE",
            portId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanReachUserManagement()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("ADMIN"));

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            email = "",
            fullName = "",
            password = "",
            role = "OPERATOR",
            status = "ACTIVE",
            portId = (Guid?)null
        });

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CannotSaveRiskThresholds()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("OPERATOR"));

        var response = await client.PutAsJsonAsync("/api/risk/thresholds", new
        {
            thresholds = Array.Empty<object>(),
            changeReason = "standard user should not configure real thresholds"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CannotCreateSopRule()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("OPERATOR"));

        var response = await client.PostAsJsonAsync("/api/sop-rules", new
        {
            ruleCode = "",
            ruleName = "",
            actionConfigText = "{"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CannotUpdatePorts()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("OPERATOR"));

        var response = await client.PutAsJsonAsync($"/api/ports/{Guid.NewGuid()}", new
        {
            code = "",
            name = "",
            address = "Denied",
            latitude = 16.1m,
            longitude = 108.2m,
            timezone = "Asia/Ho_Chi_Minh",
            weatherSource = "OPENWEATHER",
            weatherStationId = (string?)null,
            isActive = true
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
}
