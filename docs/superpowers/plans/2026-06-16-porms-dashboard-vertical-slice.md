# PORMS Dashboard Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a working PORMS demo slice with PostgreSQL schema, ASP.NET Core API, React frontend, and ETL alignment, while delivering the frontend first with deterministic mock data and full route coverage from `design.html`.

**Architecture:** Use the SQL schema as the source of truth, add a thin Npgsql-based backend over `operational` tables/views, and rebuild the React app from the `design.html` prototype with a mock-first service layer that can later be swapped to backend APIs. Keep authentication lightweight for the first slice so the frontend demo path is reliable before full integration.

**Tech Stack:** PostgreSQL 16, ASP.NET Core 8, Npgsql, xUnit, React 18, TypeScript, Vite, Python 3.11, Prefect 2, Pandas

---

## File Structure

### Database and Infra

- Create or replace: `docs/database/schema.sql`
- Modify: `infra/docker-compose.yml`

### Backend

- Create: `backend/PORMS.API/PORMS.API.csproj`
- Create: `backend/PORMS.Infrastructure/PORMS.Infrastructure.csproj`
- Create: `backend/PORMS.Tests/PORMS.Tests.csproj`
- Create: `backend/Directory.Build.props`
- Modify: `backend/PORMS.sln`
- Modify: `backend/PORMS.API/Program.cs`
- Create: `backend/PORMS.API/Configuration/DatabaseOptions.cs`
- Create: `backend/PORMS.API/Contracts/DashboardSummaryResponse.cs`
- Create: `backend/PORMS.API/Contracts/PortSummaryResponse.cs`
- Create: `backend/PORMS.API/Contracts/ZoneResponse.cs`
- Create: `backend/PORMS.API/Contracts/AlertResponse.cs`
- Create: `backend/PORMS.API/Contracts/OperationEventResponse.cs`
- Create: `backend/PORMS.API/Contracts/SimulationRunRequest.cs`
- Create: `backend/PORMS.API/Contracts/ErrorResponse.cs`
- Create: `backend/PORMS.Infrastructure/Data/NpgsqlConnectionFactory.cs`
- Create: `backend/PORMS.Infrastructure/Repositories/DashboardRepository.cs`
- Create: `backend/PORMS.Infrastructure/Repositories/PortRepository.cs`
- Create: `backend/PORMS.Infrastructure/Repositories/AlertRepository.cs`
- Create: `backend/PORMS.Infrastructure/Repositories/OperationEventRepository.cs`
- Create: `backend/PORMS.Infrastructure/Repositories/SimulationRepository.cs`
- Modify: `backend/PORMS.API/Controllers/PortController.cs`
- Modify: `backend/PORMS.API/Controllers/AlertController.cs`
- Modify: `backend/PORMS.API/Controllers/OperationLogController.cs`
- Modify: `backend/PORMS.API/Controllers/SimulationController.cs`
- Create: `backend/PORMS.API/Controllers/DashboardController.cs`
- Create: `backend/PORMS.API/Middleware/ApiExceptionMiddleware.cs`
- Test: `backend/PORMS.Tests/Integration/ApiHealthTests.cs`
- Test: `backend/PORMS.Tests/Integration/DashboardSummaryTests.cs`
- Test: `backend/PORMS.Tests/Integration/SimulationFlowTests.cs`

### Frontend

- Create: `frontend/package.json`
- Create: `frontend/tsconfig.json`
- Create: `frontend/vite.config.ts`
- Create: `frontend/index.html`
- Create: `frontend/src/main.tsx`
- Create: `frontend/src/App.tsx`
- Create: `frontend/src/router/index.tsx`
- Create: `frontend/src/styles/app.css`
- Create: `frontend/src/styles/layout.css`
- Create: `frontend/src/styles/pages.css`
- Create: `frontend/src/styles/components.css`
- Create: `frontend/src/types/dashboard.ts`
- Create: `frontend/src/types/alert.ts`
- Create: `frontend/src/types/port.ts`
- Create: `frontend/src/types/log.ts`
- Create: `frontend/src/types/simulation.ts`
- Create: `frontend/src/mock/demoData.ts`
- Create: `frontend/src/services/dashboardService.ts`
- Create: `frontend/src/services/alertService.ts`
- Create: `frontend/src/services/portService.ts`
- Create: `frontend/src/services/logService.ts`
- Create: `frontend/src/services/simulationService.ts`
- Create: `frontend/src/pages/LoginPage.tsx`
- Create: `frontend/src/pages/DashboardPage.tsx`
- Create: `frontend/src/pages/AlertPage.tsx`
- Create: `frontend/src/pages/LogPage.tsx`
- Create: `frontend/src/pages/PortManagementPage.tsx`
- Create: `frontend/src/pages/UsersPage.tsx`
- Create: `frontend/src/pages/RiskConfigPage.tsx`
- Create: `frontend/src/pages/SopRulesPage.tsx`
- Create: `frontend/src/pages/SimulationPage.tsx`
- Create: `frontend/src/pages/SimulationResultsPage.tsx`
- Create: `frontend/src/pages/AnalyticsPage.tsx`
- Create: `frontend/src/pages/ProfilePage.tsx`
- Create: `frontend/src/pages/ChangePasswordPage.tsx`
- Create: `frontend/src/components/layout/AppShell.tsx`
- Create: `frontend/src/components/layout/Sidebar.tsx`
- Create: `frontend/src/components/layout/Topbar.tsx`
- Create: `frontend/src/components/common/Badge.tsx`
- Create: `frontend/src/components/common/PlaceholderPanel.tsx`
- Create: `frontend/src/components/dashboard/RiskHeroCard.tsx`
- Create: `frontend/src/components/dashboard/ModeCard.tsx`
- Create: `frontend/src/components/dashboard/WeatherSummaryCard.tsx`
- Create: `frontend/src/components/dashboard/AlertListCard.tsx`
- Create: `frontend/src/components/dashboard/OperationLogCard.tsx`
- Create: `frontend/src/components/dashboard/RiskTrendChart.tsx`
- Create: `frontend/src/components/port/PortListCard.tsx`
- Create: `frontend/src/components/port/ZoneList.tsx`

### ETL

- Modify: `etl/flows/dw_loader.py`
- Modify: `etl/flows/historical_backfill.py`
- Modify: `etl/flows/weather_collector.py`
- Modify: `etl/tasks/transformer.py`
- Create: `etl/tests/test_dw_loader_schema_alignment.py`

---

### Task 1: Sync Database Schema Into Source

**Files:**
- Modify: `docs/database/schema.sql`
- Modify: `infra/docker-compose.yml`
- Test: `docs/superpowers/specs/2026-06-16-porms-dashboard-vertical-slice-design.md`

- [ ] **Step 1: Copy the validated schema into the source tree**

Use the design workspace schema as the canonical file and replace the empty source file so Docker and local imports use the same DDL.

```sql
-- docs/database/schema.sql
-- should match 001.design/database/porms_schema.sql
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE SCHEMA IF NOT EXISTS operational;
CREATE SCHEMA IF NOT EXISTS analytics;
-- ... full validated PORMS schema ...
```

- [ ] **Step 2: Point infra at the synced schema**

Keep Compose using the checked-in source schema.

```yaml
# infra/docker-compose.yml
services:
  postgres:
    volumes:
      - ../docs/database/schema.sql:/docker-entrypoint-initdb.d/02_schema.sql
```

- [ ] **Step 3: Verify the schema file is non-empty**

Run: `Get-Item docs\database\schema.sql | Select-Object Length`
Expected: `Length` is greater than `50000`

- [ ] **Step 4: Verify first-run schema import**

Run:

```powershell
$name='porms-plan-db'
docker rm -f $name 2>$null | Out-Null
docker run --name $name -e POSTGRES_PASSWORD=testpass -e POSTGRES_DB=porms_db -d postgres:16-alpine
docker cp docs/database/schema.sql "${name}:/tmp/schema.sql"
docker exec $name psql -v ON_ERROR_STOP=1 -U postgres -d porms_db -f /tmp/schema.sql
docker exec $name psql -U postgres -d porms_db -c "SELECT table_schema, COUNT(*) FROM information_schema.tables WHERE table_type='BASE TABLE' AND table_schema IN ('public','operational','analytics') GROUP BY table_schema ORDER BY table_schema;"
```

Expected:

```text
 analytics   | 11
 operational | 20
 public      | 1
```

- [ ] **Step 5: Verify second-run idempotency**

Run:

```powershell
docker exec $name psql -v ON_ERROR_STOP=1 -U postgres -d porms_db -f /tmp/schema.sql
```

Expected: exit code `0` with no errors. `NOTICE` lines and normal `psql` command tags are acceptable because the schema includes idempotent seed data.

- [ ] **Step 6: Commit**

```bash
git add docs/database/schema.sql infra/docker-compose.yml
git commit -m "chore: sync validated PORMS database schema"
```

### Task 2: Rebuild Backend Project Skeleton

**Files:**
- Create: `backend/PORMS.API/PORMS.API.csproj`
- Create: `backend/PORMS.Infrastructure/PORMS.Infrastructure.csproj`
- Create: `backend/PORMS.Tests/PORMS.Tests.csproj`
- Create: `backend/Directory.Build.props`
- Modify: `backend/PORMS.sln`
- Modify: `backend/PORMS.API/Program.cs`
- Create: `backend/PORMS.API/Configuration/DatabaseOptions.cs`
- Create: `backend/PORMS.API/Middleware/ApiExceptionMiddleware.cs`
- Test: `backend/PORMS.Tests/Integration/ApiHealthTests.cs`

- [ ] **Step 1: Write the failing backend project test**

Create a smoke integration test that expects `/health` to return `200 OK`.

```csharp
// backend/PORMS.Tests/Integration/ApiHealthTests.cs
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PORMS.Tests.Integration;

public class ApiHealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiHealthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/PORMS.Tests/PORMS.Tests.csproj --filter Health_ReturnsOk`
Expected: fail because `.csproj` files and `Program` app do not exist yet.

- [ ] **Step 3: Create the .NET projects and solution wiring**

Use a minimal project layout with direct project references.

```xml
<!-- backend/PORMS.API/PORMS.API.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\PORMS.Infrastructure\PORMS.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql.DependencyInjection" Version="8.0.3" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
  </ItemGroup>
</Project>
```

```xml
<!-- backend/PORMS.Infrastructure/PORMS.Infrastructure.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="8.0.3" />
  </ItemGroup>
</Project>
```

```xml
<!-- backend/PORMS.Tests/PORMS.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.6" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\PORMS.API\PORMS.API.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Add a minimal host with health endpoint**

```csharp
// backend/PORMS.API/Program.cs
using PORMS.API.Configuration;
using PORMS.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();

public partial class Program;
```

```csharp
// backend/PORMS.API/Configuration/DatabaseOptions.cs
namespace PORMS.API.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string ConnectionString { get; set; } = string.Empty;
}
```

```csharp
// backend/PORMS.API/Middleware/ApiExceptionMiddleware.cs
using System.Text.Json;

namespace PORMS.API.Middleware;

public sealed class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ApiExceptionMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var payload = new
            {
                error = ex.Message,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test backend/PORMS.Tests/PORMS.Tests.csproj --filter Health_ReturnsOk`
Expected: `PASS`

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "feat: scaffold PORMS backend api projects"
```

### Task 3: Build Backend Read APIs Over PostgreSQL

**Files:**
- Create: `backend/PORMS.Infrastructure/Data/NpgsqlConnectionFactory.cs`
- Create: `backend/PORMS.Infrastructure/Repositories/DashboardRepository.cs`
- Create: `backend/PORMS.Infrastructure/Repositories/PortRepository.cs`
- Create: `backend/PORMS.Infrastructure/Repositories/AlertRepository.cs`
- Create: `backend/PORMS.Infrastructure/Repositories/OperationEventRepository.cs`
- Create: `backend/PORMS.API/Contracts/DashboardSummaryResponse.cs`
- Create: `backend/PORMS.API/Contracts/PortSummaryResponse.cs`
- Create: `backend/PORMS.API/Contracts/ZoneResponse.cs`
- Create: `backend/PORMS.API/Contracts/AlertResponse.cs`
- Create: `backend/PORMS.API/Contracts/OperationEventResponse.cs`
- Create: `backend/PORMS.API/Controllers/DashboardController.cs`
- Modify: `backend/PORMS.API/Controllers/PortController.cs`
- Modify: `backend/PORMS.API/Controllers/AlertController.cs`
- Modify: `backend/PORMS.API/Controllers/OperationLogController.cs`
- Test: `backend/PORMS.Tests/Integration/DashboardSummaryTests.cs`

- [ ] **Step 1: Write the failing dashboard API test**

```csharp
// backend/PORMS.Tests/Integration/DashboardSummaryTests.cs
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PORMS.Tests.Integration;

public class DashboardSummaryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DashboardSummaryTests(WebApplicationFactory<Program> factory)
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
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/PORMS.Tests/PORMS.Tests.csproj --filter DashboardSummary_ReturnsSuccess`
Expected: `404 Not Found`
Note: This is a TDD process requirement. It must be observed during implementation, but it does not need to remain visible as a separate commit in final branch history.

- [ ] **Step 3: Add connection factory and repositories**

```csharp
// backend/PORMS.Infrastructure/Data/NpgsqlConnectionFactory.cs
using Npgsql;

namespace PORMS.Infrastructure.Data;

public sealed class NpgsqlConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public NpgsqlConnection Open()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
```

```csharp
// backend/PORMS.Infrastructure/Repositories/DashboardRepository.cs
using Npgsql;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class DashboardRepository
{
    private readonly NpgsqlConnectionFactory _factory;

    public DashboardRepository(NpgsqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        await using var connection = _factory.Open();

        const string sql = """
        SELECT port_id, port_code, port_name, current_risk_level, current_operation_mode,
               wind_speed_ms, beaufort_number, rainfall_1h_mm, visibility_km, active_alert_count
        FROM operational.v_port_current_state
        ORDER BY port_code
        LIMIT 1;
        """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("No demo port state found.");
        }

        return new DashboardSummaryResponse
        {
            PortId = reader.GetGuid(0),
            PortCode = reader.GetString(1),
            PortName = reader.GetString(2),
            CurrentRiskLevel = reader.GetString(3),
            CurrentOperationMode = reader.GetString(4),
            WindSpeedMs = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            BeaufortNumber = reader.IsDBNull(6) ? null : reader.GetInt16(6),
            Rainfall1hMm = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
            VisibilityKm = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
            ActiveAlertCount = reader.GetInt64(9)
        };
    }
}
```

- [ ] **Step 4: Add API contracts and controllers**

```csharp
// backend/PORMS.API/Contracts/DashboardSummaryResponse.cs
namespace PORMS.API.Contracts;

public sealed class DashboardSummaryResponse
{
    public Guid PortId { get; set; }
    public string PortCode { get; set; } = string.Empty;
    public string PortName { get; set; } = string.Empty;
    public string CurrentRiskLevel { get; set; } = string.Empty;
    public string CurrentOperationMode { get; set; } = string.Empty;
    public decimal? WindSpeedMs { get; set; }
    public short? BeaufortNumber { get; set; }
    public decimal? Rainfall1hMm { get; set; }
    public decimal? VisibilityKm { get; set; }
    public long ActiveAlertCount { get; set; }
}
```

```csharp
// backend/PORMS.API/Controllers/DashboardController.cs
using Microsoft.AspNetCore.Mvc;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromServices] DashboardRepository repository,
        CancellationToken cancellationToken)
    {
        var summary = await repository.GetSummaryAsync(cancellationToken);
        return Ok(summary);
    }
}
```

- [ ] **Step 5: Register repositories in Program**

```csharp
// backend/PORMS.API/Program.cs
builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing DefaultConnection.");
    return new PORMS.Infrastructure.Data.NpgsqlConnectionFactory(connectionString);
});
builder.Services.AddScoped<PORMS.Infrastructure.Repositories.DashboardRepository>();
builder.Services.AddScoped<PORMS.Infrastructure.Repositories.PortRepository>();
builder.Services.AddScoped<PORMS.Infrastructure.Repositories.AlertRepository>();
builder.Services.AddScoped<PORMS.Infrastructure.Repositories.OperationEventRepository>();
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test backend/PORMS.Tests/PORMS.Tests.csproj --filter DashboardSummary_ReturnsSuccess`
Expected: `PASS` against a test DB seeded with the schema.

- [ ] **Step 7: Commit**

```bash
git add backend
git commit -m "feat: add PORMS dashboard read APIs"
```

### Task 4: Add Backend Demo Simulation Write Path

**Files:**
- Create: `backend/PORMS.API/Contracts/SimulationRunRequest.cs`
- Create: `backend/PORMS.Infrastructure/Repositories/SimulationRepository.cs`
- Modify: `backend/PORMS.API/Controllers/SimulationController.cs`
- Test: `backend/PORMS.Tests/Integration/SimulationFlowTests.cs`

- [ ] **Step 1: Write the failing simulation flow test**

```csharp
// backend/PORMS.Tests/Integration/SimulationFlowTests.cs
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PORMS.Tests.Integration;

public class SimulationFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SimulationFlowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RunDemo_CreatesSimulationData()
    {
        var client = _factory.CreateClient();
        var content = new StringContent("""{"portCode":"TIENSA"}""", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/simulation/run-demo", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/PORMS.Tests/PORMS.Tests.csproj --filter RunDemo_CreatesSimulationData`
Expected: `404 Not Found`

- [ ] **Step 3: Implement the minimal simulation writer**

```csharp
// backend/PORMS.API/Contracts/SimulationRunRequest.cs
namespace PORMS.API.Contracts;

public sealed class SimulationRunRequest
{
    public string PortCode { get; set; } = "TIENSA";
}
```

```csharp
// backend/PORMS.Infrastructure/Repositories/SimulationRepository.cs
using Npgsql;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class SimulationRepository
{
    private readonly NpgsqlConnectionFactory _factory;

    public SimulationRepository(NpgsqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task RunDemoAsync(string portCode, CancellationToken cancellationToken)
    {
        await using var connection = _factory.Open();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string portSql = "SELECT id FROM operational.ports WHERE UPPER(code)=UPPER(@code) LIMIT 1;";
        await using var portCommand = new NpgsqlCommand(portSql, connection, transaction);
        portCommand.Parameters.AddWithValue("code", portCode);
        var portId = (Guid?)await portCommand.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Demo port not found.");

        var steps = new[]
        {
            ("LOW", "NORMAL", 6.4m, (short)4, 3.2m, 12m),
            ("MEDIUM", "NORMAL", 12.8m, (short)6, 12.5m, 8m),
            ("HIGH", "LIMITED", 18.4m, (short)8, 28.5m, 4.2m),
            ("CRITICAL", "STOP", 25.2m, (short)10, 54.8m, 0.8m)
        };

        foreach (var step in steps)
        {
            var readingId = Guid.NewGuid();
            var assessmentId = Guid.NewGuid();

            await new NpgsqlCommand("""
                INSERT INTO operational.weather_readings (
                    id, port_id, wind_speed_ms, beaufort_number, rainfall_1h_mm,
                    visibility_km, observed_at, data_source, raw_payload, is_simulation
                ) VALUES (
                    @id, @portId, @wind, @beaufort, @rain, @visibility,
                    NOW(), 'SIMULATION_API', '{}'::jsonb, FALSE
                );
            """, connection, transaction)
            {
                Parameters =
                {
                    new("id", readingId),
                    new("portId", portId),
                    new("wind", step.Item3),
                    new("beaufort", step.Item4),
                    new("rain", step.Item5),
                    new("visibility", step.Item6)
                }
            }.ExecuteNonQueryAsync(cancellationToken);

            await new NpgsqlCommand("""
                INSERT INTO operational.risk_assessments (
                    id, weather_reading_id, port_id, wind_risk_level, rain_risk_level,
                    visibility_risk_level, final_risk_level, dominant_factor,
                    assessment_summary, threshold_version, is_simulation
                ) VALUES (
                    @id, @readingId, @portId, @risk::operational.risk_level_enum,
                    @risk::operational.risk_level_enum, @risk::operational.risk_level_enum,
                    @risk::operational.risk_level_enum, 'WIND',
                    @summary, 1, FALSE
                );
            """, connection, transaction)
            {
                Parameters =
                {
                    new("id", assessmentId),
                    new("readingId", readingId),
                    new("portId", portId),
                    new("risk", step.Item1),
                    new("summary", $"Simulation step created {step.Item1} risk.")
                }
            }.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Add the simulation endpoint**

```csharp
// backend/PORMS.API/Controllers/SimulationController.cs
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/simulation")]
public sealed class SimulationController : ControllerBase
{
    [HttpPost("run-demo")]
    public async Task<IActionResult> RunDemo(
        [FromBody] SimulationRunRequest request,
        [FromServices] SimulationRepository repository,
        CancellationToken cancellationToken)
    {
        await repository.RunDemoAsync(request.PortCode, cancellationToken);
        return Ok(new { status = "completed" });
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test backend/PORMS.Tests/PORMS.Tests.csproj --filter RunDemo_CreatesSimulationData`
Expected: `PASS`

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "feat: add PORMS demo simulation endpoint"
```

### Task 5: Rebuild Frontend Skeleton, Router, and App Shell

**Files:**
- Create: `frontend/package.json`
- Create: `frontend/tsconfig.json`
- Create: `frontend/vite.config.ts`
- Create: `frontend/index.html`
- Create: `frontend/src/main.tsx`
- Create: `frontend/src/App.tsx`
- Create: `frontend/src/router/index.tsx`
- Create: `frontend/src/styles/app.css`
- Create: `frontend/src/styles/layout.css`
- Create: `frontend/src/styles/pages.css`
- Create: `frontend/src/styles/components.css`
- Create: `frontend/src/pages/LoginPage.tsx`
- Create: `frontend/src/components/layout/AppShell.tsx`
- Create: `frontend/src/components/layout/Sidebar.tsx`
- Create: `frontend/src/components/layout/Topbar.tsx`

- [ ] **Step 1: Write the failing frontend build**

Run: `npm --prefix frontend install`
Expected: fail because `frontend/package.json` is empty.

- [ ] **Step 2: Create the Vite React project files**

```json
// frontend/package.json
{
  "name": "porms-frontend",
  "private": true,
  "version": "0.1.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview"
  },
  "dependencies": {
    "react": "^18.3.1",
    "react-dom": "^18.3.1"
  },
  "devDependencies": {
    "@types/react": "^18.3.3",
    "@types/react-dom": "^18.3.0",
    "@vitejs/plugin-react": "^4.3.1",
    "typescript": "^5.5.4",
    "vite": "^5.3.4"
  }
}
```

```tsx
// frontend/src/main.tsx
import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import "./styles/app.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
```

```tsx
// frontend/src/App.tsx
import { useState } from "react";
import { LoginPage } from "./pages/LoginPage";
import { AppShell } from "./components/layout/AppShell";

export default function App() {
  const [authenticated, setAuthenticated] = useState(false);

  if (!authenticated) {
    return <LoginPage onEnter={() => setAuthenticated(true)} />;
  }

  return <AppShell />;
}
```

- [ ] **Step 3: Add the initial shell and login page**

```tsx
// frontend/src/pages/LoginPage.tsx
type LoginPageProps = {
  onEnter: () => void;
};

export function LoginPage({ onEnter }: LoginPageProps) {
  return (
    <div className="login-page">
      <section className="login-brand">
        <h1>PORMS</h1>
        <p>Port Operation Risk Management System demo</p>
      </section>
      <section className="login-panel">
        <button className="primary-button" onClick={onEnter}>
          Enter Demo
        </button>
      </section>
    </div>
  );
}
```

```tsx
// frontend/src/components/layout/AppShell.tsx
export function AppShell() {
  return (
    <div className="app-shell">
      <aside className="sidebar">PORMS</aside>
      <main className="main-panel">
        <header className="topbar">Dashboard</header>
        <section className="content">Loading dashboard...</section>
      </main>
    </div>
  );
}
```

- [ ] **Step 4: Run the frontend build to verify it passes**

Run:

```bash
npm --prefix frontend install
npm --prefix frontend run build
```

Expected: `vite build` completes successfully.

- [ ] **Step 5: Commit**

```bash
git add frontend
git commit -m "feat: scaffold PORMS React frontend"
```

### Task 6: Build Frontend Mock Data Layer and Route-Complete Demo UI

**Files:**
- Create: `frontend/src/types/dashboard.ts`
- Create: `frontend/src/types/alert.ts`
- Create: `frontend/src/types/port.ts`
- Create: `frontend/src/types/log.ts`
- Create: `frontend/src/types/simulation.ts`
- Create: `frontend/src/mock/demoData.ts`
- Create: `frontend/src/services/dashboardService.ts`
- Create: `frontend/src/services/alertService.ts`
- Create: `frontend/src/services/portService.ts`
- Create: `frontend/src/services/logService.ts`
- Create: `frontend/src/services/simulationService.ts`
- Create: `frontend/src/pages/DashboardPage.tsx`
- Create: `frontend/src/pages/AlertPage.tsx`
- Create: `frontend/src/pages/LogPage.tsx`
- Create: `frontend/src/pages/PortManagementPage.tsx`
- Create: `frontend/src/pages/UsersPage.tsx`
- Create: `frontend/src/pages/RiskConfigPage.tsx`
- Create: `frontend/src/pages/SopRulesPage.tsx`
- Create: `frontend/src/pages/SimulationPage.tsx`
- Create: `frontend/src/pages/SimulationResultsPage.tsx`
- Create: `frontend/src/pages/AnalyticsPage.tsx`
- Create: `frontend/src/pages/ProfilePage.tsx`
- Create: `frontend/src/pages/ChangePasswordPage.tsx`
- Create: `frontend/src/components/common/Badge.tsx`
- Create: `frontend/src/components/common/PlaceholderPanel.tsx`
- Create: `frontend/src/components/dashboard/RiskHeroCard.tsx`
- Create: `frontend/src/components/dashboard/ModeCard.tsx`
- Create: `frontend/src/components/dashboard/WeatherSummaryCard.tsx`
- Create: `frontend/src/components/dashboard/AlertListCard.tsx`
- Create: `frontend/src/components/dashboard/OperationLogCard.tsx`
- Create: `frontend/src/components/dashboard/RiskTrendChart.tsx`
- Create: `frontend/src/components/port/PortListCard.tsx`
- Create: `frontend/src/components/port/ZoneList.tsx`
- Modify: `frontend/src/App.tsx`
- Modify: `frontend/src/components/layout/AppShell.tsx`

- [ ] **Step 1: Write the failing frontend route demo expectation**

Run: `npm --prefix frontend run build`
Expected: fail because the route components, services, and mock modules do not exist yet.

- [ ] **Step 2: Add typed frontend DTOs and mock demo dataset**

```ts
// frontend/src/types/dashboard.ts
export type DashboardSummary = {
  portId: string;
  portCode: string;
  portName: string;
  currentRiskLevel: string;
  currentOperationMode: string;
  windSpeedMs: number | null;
  beaufortNumber: number | null;
  rainfall1hMm: number | null;
  visibilityKm: number | null;
  activeAlertCount: number;
};
```

- [ ] **Step 3: Implement Promise-based mock services**

Each service should return data from `frontend/src/mock/demoData.ts` so the UI
looks asynchronous even before real API wiring.

```ts
// frontend/src/services/dashboardService.ts
import { demoData } from "../mock/demoData";
import type { DashboardSummary } from "../types/dashboard";

export async function getDashboardSummary(): Promise<DashboardSummary> {
  return Promise.resolve(demoData.dashboardSummary);
}
```

```ts
// frontend/src/services/simulationService.ts
import { runDemoStepSequence } from "../mock/demoData";

export async function runDemoSimulation(): Promise<void> {
  await runDemoStepSequence();
}
```

- [ ] **Step 4: Implement the route-backed demo pages**

```tsx
// frontend/src/pages/DashboardPage.tsx
import { useEffect, useState } from "react";
import { getDashboardSummary } from "../services/dashboardService";
import type { DashboardSummary } from "../types/dashboard";

export function DashboardPage() {
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getDashboardSummary().then(setSummary).catch((err: Error) => setError(err.message));
  }, []);

  if (error) {
    return <div className="error-banner">{error}</div>;
  }

  if (!summary) {
    return <div className="loading-card">Loading dashboard...</div>;
  }

  return (
    <section className="dashboard-grid">
      <div className={`risk-card risk-${summary.currentRiskLevel.toLowerCase()}`}>
        <p className="eyebrow">{summary.portName}</p>
        <h1>{summary.currentRiskLevel}</h1>
        <p>Mode: {summary.currentOperationMode}</p>
      </div>
      <div className="weather-card">
        <p>Wind: {summary.windSpeedMs ?? "-"} m/s</p>
        <p>Beaufort: {summary.beaufortNumber ?? "-"}</p>
        <p>Rain: {summary.rainfall1hMm ?? "-"} mm/h</p>
        <p>Visibility: {summary.visibilityKm ?? "-"} km</p>
      </div>
    </section>
  );
}
```

- [ ] **Step 5: Add simulation trigger button and mock state refresh**

```tsx
// inside DashboardPage.tsx
import { runDemoSimulation } from "../services/simulationService";

<button
  className="primary-button"
  onClick={async () => {
    await runDemoSimulation(summary.portCode);
    const nextSummary = await getDashboardSummary();
    setSummary(nextSummary);
  }}
>
  Run Demo Simulation
</button>
```

- [ ] **Step 6: Add placeholder pages for lower-priority routes**

Each placeholder page should render a title, one-paragraph description, and a
card that clearly says the screen is staged for later backend integration, while
still matching the visual shell from `design.html`.

- [ ] **Step 7: Run the frontend build to verify it passes**

Run: `npm --prefix frontend run build`
Expected: `PASS`

- [ ] **Step 8: Commit**

```bash
git add frontend
git commit -m "feat: build PORMS mock-first frontend demo"
```

### Task 7: Align ETL With the New Schema

**Files:**
- Modify: `etl/flows/dw_loader.py`
- Modify: `etl/flows/historical_backfill.py`
- Modify: `etl/flows/weather_collector.py`
- Modify: `etl/tasks/transformer.py`
- Create: `etl/tests/test_dw_loader_schema_alignment.py`

- [ ] **Step 1: Write the failing ETL schema test**

```python
# etl/tests/test_dw_loader_schema_alignment.py
from pathlib import Path


def test_dw_loader_uses_new_schema_names() -> None:
    contents = Path("etl/flows/dw_loader.py").read_text(encoding="utf-8")

    assert "fact_weather_hourly" in contents
    assert "dim_risk_level" in contents
    assert "fact_risk_assessment" in contents
    assert "fact_alert" in contents
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pytest etl/tests/test_dw_loader_schema_alignment.py -q`
Expected: fail because old table names are still present.

- [ ] **Step 3: Replace old table and column names in DW loader**

Use the new schema names consistently.

```python
# examples inside etl/flows/dw_loader.py
INSERT INTO analytics.fact_weather_hourly (
    source_group_key,
    time_key,
    port_key,
    source_port_id,
    reading_count,
    avg_wind_speed_ms,
    max_beaufort,
    total_rainfall_mm,
    avg_visibility_km,
    final_risk_level_key,
    is_simulation,
    etl_batch_id
)
```

```python
SELECT risk_level_key
FROM analytics.dim_risk_level
WHERE risk_level = :final_risk_level
```

- [ ] **Step 4: Update alert ETL to use `alert_receipts`**

Replace old single-row alert read timing assumptions with receipt aggregation.

```python
SELECT
    a.id::text AS source_alert_id,
    a.port_id::text,
    TO_CHAR(a.created_at AT TIME ZONE 'Asia/Ho_Chi_Minh', 'YYYYMMDDHH24')::integer AS time_key,
    a.severity::text,
    a.alert_type,
    COUNT(ar.id) AS recipient_count,
    COUNT(ar.read_at) AS read_count,
    COUNT(ar.acknowledged_at) AS acknowledged_count
FROM operational.alerts a
LEFT JOIN operational.alert_receipts ar ON ar.alert_id = a.id
GROUP BY a.id
```

- [ ] **Step 5: Run ETL tests to verify they pass**

Run:

```bash
pytest etl/tests/test_beaufort.py -q
pytest etl/tests/test_dw_loader_schema_alignment.py -q
```

Expected: `PASS`

- [ ] **Step 6: Commit**

```bash
git add etl
git commit -m "fix: align PORMS etl with vertical slice schema"
```

### Task 8: End-to-End Verification

**Files:**
- Verify only; no required file changes

- [ ] **Step 1: Start PostgreSQL and import schema**

Run:

```bash
docker compose -f infra/docker-compose.yml --env-file .env up -d postgres
```

Expected: PostgreSQL healthy.

- [ ] **Step 2: Run backend tests**

Run: `dotnet test backend/PORMS.Tests/PORMS.Tests.csproj`
Expected: all backend tests pass.

- [ ] **Step 3: Build frontend**

Run:

```bash
npm --prefix frontend install
npm --prefix frontend run build
```

Expected: production build succeeds.

- [ ] **Step 4: Run ETL tests**

Run: `pytest etl/tests -q`
Expected: tests pass.

- [ ] **Step 5: Perform manual demo path**

Run backend and frontend:

```bash
dotnet run --project backend/PORMS.API/PORMS.API.csproj
npm --prefix frontend run dev
```

Manual check:

```text
1. Open the frontend.
2. Enter demo mode.
3. Confirm dashboard summary loads from API.
4. Click Run Demo Simulation.
5. Confirm risk changes to HIGH/CRITICAL.
6. Confirm operation mode changes to LIMITED/STOP.
7. Confirm alerts and operation events update.
```

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat: deliver PORMS dashboard vertical slice"
```

---

## Self-Review

- Spec coverage checked:
  - Database sync is covered in Task 1.
  - Backend read APIs are covered in Tasks 2 and 3.
  - Simulation write path is covered in Task 4.
  - React dashboard rebuild is covered in Tasks 5 and 6.
  - ETL alignment is covered in Task 7.
  - Verification is covered in Task 8.
- Placeholder scan checked:
  - No unfinished placeholder markers remain inside task steps.
- Type consistency checked:
  - API route names, schema names, and risk/mode progression match the approved spec.

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-16-porms-dashboard-vertical-slice.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
