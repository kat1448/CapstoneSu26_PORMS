# PORMS Production API Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace frontend mock-only data access with production-shaped backend APIs backed by PostgreSQL, while keeping the demo reliable through tested contracts and explicit fallback behavior.

**Architecture:** Backend exposes stable read-model endpoints under `/api/*` using Npgsql repositories over the `operational` schema. Frontend services call a small typed API client configured by `VITE_API_BASE_URL`, map backend DTOs into UI DTOs, and fall back to existing mock data only when explicitly enabled. Integration is verified with Docker PostgreSQL on host port `55432`, backend integration tests, and a frontend production build.

**Tech Stack:** ASP.NET Core 10, Npgsql, PostgreSQL 16, xUnit, React 18, TypeScript, Vite, Docker Compose

---

## File Structure

### Backend

- Modify: `backend/PORMS.API/Program.cs`  
  Register CORS, JSON options if needed, and repositories for new read models.
- Create: `backend/PORMS.API/Configuration/CorsOptions.cs`  
  Holds allowed frontend origins for dev/prod configuration.
- Create: `backend/PORMS.API/Contracts/UserSummaryResponse.cs`  
  DTO for Users page.
- Create: `backend/PORMS.API/Contracts/WeatherSnapshotResponse.cs`  
  DTO for Dashboard weather card.
- Create: `backend/PORMS.API/Contracts/RiskTrendPointResponse.cs`  
  DTO for Dashboard risk trend.
- Create: `backend/PORMS.API/Contracts/SimulationSnapshotResponse.cs`  
  DTO for Simulation page current snapshot.
- Modify: `backend/PORMS.API/Contracts/PortSummaryResponse.cs`  
  Add UI-supporting read-model fields.
- Modify: `backend/PORMS.API/Contracts/ZoneResponse.cs`  
  Add UI-supporting read-model fields.
- Modify: `backend/PORMS.API/Contracts/AlertResponse.cs`  
  Add UI-supporting read-model fields.
- Modify: `backend/PORMS.API/Contracts/OperationEventResponse.cs`  
  Add UI-supporting read-model fields.
- Create: `backend/PORMS.Infrastructure/Repositories/UserRepository.cs`  
  Query users and assigned ports from `operational.users`.
- Create: `backend/PORMS.Infrastructure/Repositories/WeatherRepository.cs`  
  Query current weather from latest reading or current port state.
- Create: `backend/PORMS.Infrastructure/Repositories/RiskRepository.cs`  
  Query recent risk trend points from `operational.risk_assessments`.
- Modify: `backend/PORMS.Infrastructure/Repositories/SimulationRepository.cs`  
  Keep write path and add current simulation snapshot read path if useful.
- Modify: `backend/PORMS.Infrastructure/Repositories/PortRepository.cs`  
  Fill frontend read-model fields for ports and zones.
- Modify: `backend/PORMS.Infrastructure/Repositories/AlertRepository.cs`  
  Fill frontend read-model fields for alerts.
- Modify: `backend/PORMS.Infrastructure/Repositories/OperationEventRepository.cs`  
  Fill frontend read-model fields for operation log.
- Modify/Create controllers:
  - `backend/PORMS.API/Controllers/UserController.cs`
  - `backend/PORMS.API/Controllers/WeatherController.cs`
  - `backend/PORMS.API/Controllers/RiskController.cs`
  - `backend/PORMS.API/Controllers/SimulationController.cs`
- Test:
  - `backend/PORMS.Tests/Integration/ProductionApiContractTests.cs`
  - existing backend integration tests.

### Frontend

- Create/Modify: `frontend/src/services/api.ts`  
  Typed `requestJson` helper, API base URL, and fallback switch.
- Modify:
  - `frontend/src/services/dashboardService.ts`
  - `frontend/src/services/portService.ts`
  - `frontend/src/services/alertService.ts`
  - `frontend/src/services/logService.ts`
  - `frontend/src/services/simulationService.ts`
  - `frontend/src/services/userService.ts`
- Modify: `frontend/vite.config.ts` only if dev proxy is needed; prefer direct `VITE_API_BASE_URL`.
- Modify: `frontend/.env.example` if it exists; otherwise document variables in final notes.

---

## Task 1: Backend API Foundation and CORS

**Files:**
- Create: `backend/PORMS.API/Configuration/CorsOptions.cs`
- Modify: `backend/PORMS.API/Program.cs`
- Test: `backend/PORMS.Tests/Integration/ApiHealthTests.cs`

- [x] **Step 1: Write the CORS preflight test**

Add this test to `ApiHealthTests.cs`:

```csharp
[Fact]
public async Task Cors_Preflight_AllowsFrontendOrigin()
{
    var client = _factory.CreateClient();
    using var request = new HttpRequestMessage(HttpMethod.Options, "/api/dashboard/summary");
    request.Headers.Add("Origin", "http://localhost:5173");
    request.Headers.Add("Access-Control-Request-Method", "GET");

    var response = await client.SendAsync(request);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
    Assert.Contains("http://localhost:5173", origins);
}
```

- [x] **Step 2: Run the test and verify it fails**

Run:

```powershell
dotnet test backend/PORMS.Tests/PORMS.Tests.csproj --filter Cors_Preflight_AllowsFrontendOrigin
```

Expected: fail because CORS is not configured.

- [x] **Step 3: Add CORS configuration**

Create `CorsOptions.cs`:

```csharp
namespace PORMS.API.Configuration;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public string[] AllowedOrigins { get; set; } = ["http://localhost:5173"];
}
```

Update `Program.cs`:

```csharp
builder.Services.Configure<CorsOptions>(
    builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var configured = builder.Configuration
            .GetSection($"{CorsOptions.SectionName}:AllowedOrigins")
            .Get<string[]>();

        var origins = configured is { Length: > 0 }
            ? configured
            : ["http://localhost:5173"];

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
```

Then call `app.UseCors("Frontend");` before `app.MapControllers();`.

- [x] **Step 4: Run the test and verify it passes**

Run:

```powershell
dotnet test backend/PORMS.Tests/PORMS.Tests.csproj --filter Cors_Preflight_AllowsFrontendOrigin
```

Expected: PASS.

---

## Task 2: Backend Read-Model Contracts for Frontend

**Files:**
- Modify: `backend/PORMS.API/Contracts/PortSummaryResponse.cs`
- Modify: `backend/PORMS.API/Contracts/ZoneResponse.cs`
- Modify: `backend/PORMS.API/Contracts/AlertResponse.cs`
- Modify: `backend/PORMS.API/Contracts/OperationEventResponse.cs`
- Create: `backend/PORMS.API/Contracts/UserSummaryResponse.cs`
- Create: `backend/PORMS.API/Contracts/WeatherSnapshotResponse.cs`
- Create: `backend/PORMS.API/Contracts/RiskTrendPointResponse.cs`
- Create: `backend/PORMS.API/Contracts/SimulationSnapshotResponse.cs`
- Test: `backend/PORMS.Tests/Integration/ProductionApiContractTests.cs`

- [x] **Step 1: Write contract tests that assert frontend fields exist**

Create tests that call `/api/ports`, `/api/alerts`, `/api/operation-events`, and assert JSON contains `updatedAtLabel`, `read`, and `tone`.

- [x] **Step 2: Add DTO fields**

Add nullable or defaulted fields without breaking existing tests:

```csharp
public string UpdatedAtLabel { get; set; } = string.Empty;
public string CapacityLabel { get; set; } = string.Empty;
public string StatusLabel { get; set; } = string.Empty;
public bool OverrideEnabled { get; set; }
public bool Read { get; set; }
public string Tone { get; set; } = "info";
```

- [x] **Step 3: Populate fields in repositories**

Use deterministic labels such as `"Vừa cập nhật"`, `"Bình thường"`, `"Hạn chế"`, and tone mapping based on risk/severity.

- [x] **Step 4: Run read API tests**

Run:

```powershell
$env:PORMS_TEST_DB_CONNECTION='Host=127.0.0.1;Port=55432;Database=porms_db;Username=postgres;Password=StrongPass123!;Include Error Detail=true'
dotnet test backend/PORMS.Tests/PORMS.Tests.csproj --filter "ReadApiSmokeTests|ProductionApiContractTests"
```

Expected: PASS.

---

## Task 3: Missing Backend Endpoints

**Files:**
- Create: `backend/PORMS.Infrastructure/Repositories/UserRepository.cs`
- Create: `backend/PORMS.Infrastructure/Repositories/WeatherRepository.cs`
- Create: `backend/PORMS.Infrastructure/Repositories/RiskRepository.cs`
- Modify: `backend/PORMS.Infrastructure/Repositories/SimulationRepository.cs`
- Modify: `backend/PORMS.API/Controllers/UserController.cs`
- Modify: `backend/PORMS.API/Controllers/WeatherController.cs`
- Modify: `backend/PORMS.API/Controllers/RiskController.cs`
- Modify: `backend/PORMS.API/Controllers/SimulationController.cs`
- Modify: `backend/PORMS.API/Program.cs`
- Test: `backend/PORMS.Tests/Integration/ProductionApiContractTests.cs`

- [x] **Step 1: Write endpoint smoke tests**

Assert these endpoints return `200 OK`:

```text
GET /api/users
GET /api/weather/current
GET /api/risk/trend
GET /api/simulation/current
```

- [x] **Step 2: Implement repositories**

Use SQL against existing schema:

```sql
SELECT u.id, u.email, u.full_name, u.role, u.status, p.name AS port_name, u.last_login_at
FROM operational.users u
LEFT JOIN operational.ports p ON p.id = u.assigned_port_id
WHERE u.deleted_at IS NULL
ORDER BY u.full_name;
```

For weather, use `operational.v_port_current_state` and default null values to `0` only at DTO boundary.

For risk trend, aggregate latest assessments; if there are no rows, return a stable four-point LOW baseline.

For simulation current, return latest session summary if present; otherwise `IDLE` from current dashboard state.

- [x] **Step 3: Implement controllers**

Controllers should be thin: inject repository, return `Ok(result)`, and use existing exception middleware.

- [x] **Step 4: Register repositories**

Register new repositories in `Program.cs` with scoped lifetime.

- [x] **Step 5: Run full backend tests**

Run:

```powershell
$env:PORMS_TEST_DB_CONNECTION='Host=127.0.0.1;Port=55432;Database=porms_db;Username=postgres;Password=StrongPass123!;Include Error Detail=true'
dotnet test backend/PORMS.Tests/PORMS.Tests.csproj --no-build
```

Expected: PASS.

---

## Task 4: Frontend API Client and Service Integration

**Files:**
- Modify: `frontend/src/services/api.ts`
- Modify frontend service files listed above
- Test: `frontend/src/services/*.ts` by TypeScript build

- [x] **Step 1: Implement API client**

Use:

```ts
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000";
const USE_MOCK_FALLBACK = import.meta.env.VITE_USE_MOCK_FALLBACK !== "false";
```

`requestJson<T>(path)` should throw on non-2xx and return parsed JSON.

- [x] **Step 2: Replace mock-only services**

Each service should call API first and use mock fallback only if `VITE_USE_MOCK_FALLBACK` is not `"false"`.

- [x] **Step 3: Map backend DTOs to UI DTOs**

Map server field names directly where possible; fill UI-only defaults in service layer.

- [x] **Step 4: Build frontend**

Run:

```powershell
cd frontend
npm run build
```

Expected: PASS.

---

## Task 5: End-to-End Production-Like Verification

**Files:**
- Verify only unless config defects are found.

- [x] **Step 1: Start database and backend**

Run:

```powershell
docker compose -f infra/docker-compose.yml --env-file .env up -d postgres backend
```

- [x] **Step 2: Verify backend health and APIs**

Run:

```powershell
Invoke-WebRequest http://localhost:5000/health
Invoke-WebRequest http://localhost:5000/api/dashboard/summary
Invoke-WebRequest http://localhost:5000/api/users
```

Expected: HTTP 200.

- [x] **Step 3: Run backend tests**

Run:

```powershell
$env:PORMS_TEST_DB_CONNECTION='Host=127.0.0.1;Port=55432;Database=porms_db;Username=postgres;Password=StrongPass123!;Include Error Detail=true'
dotnet test backend/PORMS.Tests/PORMS.Tests.csproj
```

Expected: PASS.

- [x] **Step 4: Build frontend with API mode**

Run:

```powershell
cd frontend
$env:VITE_API_BASE_URL='http://localhost:5000'
$env:VITE_USE_MOCK_FALLBACK='false'
npm run build
```

Expected: PASS.

---

## Self-Review

- Spec coverage: Covers backend API gaps, CORS, frontend service integration, and E2E verification.
- Placeholder scan: No TODO/TBD placeholders remain.
- Type consistency: DTO names and route names match existing PORMS route style and frontend service names.

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-17-porms-production-api-integration.md`. Because the user requested sequential production implementation, execute inline task-by-task with verification checkpoints.
