# PORMS Dashboard Vertical Slice Design

## Objective

Implement the first real vertical slice of PORMS from the existing
`001.design/design.html` prototype into the source project:

```text
PostgreSQL -> Backend API -> React Dashboard -> ETL demo data flow
```

This slice must produce a running demo backed by real PostgreSQL data, not just
static UI state. It intentionally avoids trying to complete every PORMS module
in one pass.

## Current State

The design prototype at:

```text
D:\14.Business\007.fpt_support\001.design\design.html
```

contains the intended UI layout, demo screens, navigation, fake state, alerts,
risk changes, operation mode changes, and simulation behavior.

The source project at:

```text
D:\14.Business\007.fpt_support\000.code\CapstoneSu26_PORMS
```

already has folders for:

- ASP.NET Core backend
- React frontend
- Python/Prefect ETL
- PostgreSQL/Docker infrastructure

However, many backend and frontend files are currently empty placeholders. The
ETL folder contains real Python code, but parts of it target the older schema
names and need to be aligned with the new schema.

## Scope

### In Scope

Build a working demo path covering:

- PostgreSQL schema installation.
- Backend API for dashboard, ports, zones, alerts, operation events, and demo
  simulation.
- React frontend using the look and interaction model from `design.html`.
- ETL/demo flow that writes weather, risk, alert, and operation event data into
  PostgreSQL.
- Verification commands for database import, backend build/tests, frontend
  build, and ETL tests.

### Out of Scope for This Slice

These are deferred to later slices:

- Full JWT authentication and refresh token flow.
- Complete user management CRUD.
- Full SOP configuration CRUD.
- Full risk threshold editing UI.
- Metabase signed embedding.
- Production notification delivery such as email, SMS, or WebSocket push.
- Full OpenWeather production scheduling.
- Fine-grained RBAC enforcement on every endpoint.

The demo may use a lightweight demo login or role selector first. This keeps the
first slice focused on a real data path instead of getting trapped in auth
plumbing.

## Database Design

Use the fresh schema file from the design workspace as the source of truth:

```text
D:\14.Business\007.fpt_support\001.design\database\porms_schema.sql
```

Copy or sync this schema into the source project under:

```text
docs/database/schema.sql
```

The schema creates:

- `operational`: realtime application data.
- `analytics`: warehouse and BI data.
- `public.schema_migrations`: schema version tracking.

The Docker PostgreSQL service must initialize from `docs/database/schema.sql`.

Expected table counts after import:

```text
operational: 20
analytics:   11
public:       1
```

## Backend Design

Rebuild the backend as a minimal ASP.NET Core 8 Web API that reads and writes
the existing PostgreSQL schema directly.

### Backend Responsibilities

- Expose API endpoints consumed by the dashboard.
- Read current port state from `operational.v_port_current_state`.
- Read zones, alerts, and operation events from `operational`.
- Insert demo simulation records when requested.
- Keep SQL explicit and simple for the first slice.

Entity Framework can be introduced later if useful, but the first slice should
prefer a small Npgsql data access layer because the database schema already
exists and is not EF-generated.

### Initial Endpoints

```http
GET /health
GET /api/dashboard/summary
GET /api/ports
GET /api/ports/{portId}/zones
GET /api/alerts
GET /api/operation-events
POST /api/simulation/run-demo
```

### Dashboard Summary Response

`GET /api/dashboard/summary` returns the main dashboard state:

- Current port.
- Current risk level.
- Current operation mode.
- Latest weather values.
- Active alert count.
- Open task count.
- Recent alerts.
- Recent operation events.
- Risk trend points for display.

### Simulation Endpoint

`POST /api/simulation/run-demo` creates a deterministic demo progression:

```text
LOW -> MEDIUM -> HIGH -> CRITICAL
```

For each step it writes:

- `operational.weather_readings`
- `operational.risk_assessments`
- `operational.alerts` when risk becomes high enough
- `operational.operation_mode_logs` when mode changes
- `operational.operation_events`

Database triggers update:

- `ports.current_risk_level`
- `ports.current_operation_mode`

The endpoint can run synchronously for the first slice. A background job or
queue can come later.

## Frontend Design

Rebuild the React/Vite frontend using `design.html` as the visual reference.

### Frontend Responsibilities

- Render the demo shell, sidebar, top bar, and full route structure from
  `design.html`.
- Use typed mock services first so the UI can be demoed before the backend API
  is fully wired.
- Keep the service layer shaped like the future backend API contracts so the
  data source can be swapped later with minimal UI churn.
- Trigger a deterministic frontend demo simulation.
- Refresh in-memory mock state after simulation so the user sees updated risk,
  mode, alerts, ports, and operation logs immediately.

### Initial Screens

Implement these first as real routes:

- Demo login or role selector.
- Dashboard.
- Alerts.
- Operation Log.
- Ports/Zones read-only page.

Also create route-backed placeholder pages for:

- Users.
- Risk Configuration.
- SOP Rules.
- Simulation Results.
- Analytics.
- Profile.
- Change Password.

These pages do not need full CRUD yet, but they should preserve the layout and
visual rhythm of `design.html` so the demo feels complete.

### UI Source

Use the visual language from `design.html`:

- Navy sidebar.
- Light dashboard surface.
- Risk hero card.
- Operation mode card.
- Alert list.
- Operation timeline.
- Badges for `LOW`, `MEDIUM`, `HIGH`, `CRITICAL`, `NORMAL`, `LIMITED`, and
  `STOP`.

The first implementation should favor working React components, route coverage,
and faithful styling over pixel-perfect conversion of every prototype screen.

### Frontend Data Strategy

The frontend should be built in a mock-first mode before backend integration.

Use a small data layer such as:

```text
page -> service -> mock data module -> typed response -> component
```

The service interfaces should align with the intended backend endpoints:

- `getDashboardSummary()`
- `getAlerts()`
- `getOperationEvents()`
- `getPorts()`
- `getPortZones(portId)`
- `runDemoSimulation()`

In the first frontend slice, these functions return `Promise` values backed by
local mock modules instead of network requests.

### Frontend Simulation Behavior

The mock frontend should support a deterministic demo progression inspired by
`design.html`:

```text
MEDIUM or HIGH starting state
    -> run demo
        -> risk increases
        -> mode changes NORMAL -> LIMITED -> STOP
        -> alerts grow
        -> operation log receives new events
        -> ports/zones update badges
```

This gives the user a convincing demo before backend coupling is complete.

## ETL Design

Align ETL with the new database schema.

### Immediate ETL Fixes

The current ETL references older table names such as:

- `analytics.fact_weather_readings`
- `analytics.dim_risk_levels`
- old alert `read_at` columns
- old SOP and operation mode column names

The slice must update ETL to match:

- `analytics.fact_weather_hourly`
- `analytics.dim_risk_level`
- `analytics.fact_risk_assessment`
- `analytics.fact_sop_execution`
- `analytics.fact_alert`
- `analytics.fact_operation_event`
- `analytics.etl_watermarks`

### Demo Data Flow

For the first slice, ETL should support a deterministic demo/backfill path using
the existing storm dataset where possible. The goal is to produce database rows
that the backend can show, not to perfect the whole production ETL schedule.

## Data Flow

Main dashboard flow:

```text
React Dashboard
    -> dashboardService
        -> mock data module
            -> typed response
                -> UI cards, chart, alerts, logs
```

Demo simulation flow:

```text
User clicks "Run Demo Simulation"
    -> runDemoSimulation()
        -> update mock weather, risk, mode, alerts, ports, zones, logs
            -> frontend rerenders current route state
```

Analytics flow:

```text
Operational tables
    -> ETL flow
        -> analytics dimensions and facts
```

## Error Handling

Backend API should return structured errors:

```json
{
  "error": "message",
  "traceId": "request trace id"
}
```

The first slice should handle:

- Missing port.
- Database connection failure.
- Simulation insert failure.
- Invalid route parameters.

Frontend should show a compact error banner or toast when API calls fail.

In the mock-first frontend slice, the same error UI should be used for simulated
service failures so the UX path stays the same after API integration.

ETL should log failed flow names and update `analytics.etl_watermarks` with
`FAILED` and `error_detail` when practical.

## Testing and Verification

Minimum verification before considering the slice complete:

- Database schema imports successfully into PostgreSQL 16.
- Running schema twice remains safe.
- Backend builds.
- Backend endpoint tests cover at least:
  - `GET /health`
  - dashboard summary shape
  - demo simulation creates risk and alert data
- Frontend route shell builds.
- Frontend mock simulation updates visible dashboard state.
- Frontend builds.
- ETL tests pass.
- Manual demo path works:
  - Open frontend.
  - See dashboard.
  - Run demo simulation.
  - See risk/mode/alerts/logs change from mock-backed data.

## Implementation Order

1. Sync database schema into source project and verify PostgreSQL import.
2. Rebuild backend minimal API and data access.
3. Add backend demo simulation endpoint.
4. Rebuild the frontend shell, routes, and mock data flow from `design.html`.
5. Swap frontend services from mock data to backend APIs later without changing
   the page structure.
6. Align ETL table/column names with the new schema.
7. Run verification commands.

## Risks and Decisions

- Backend and frontend have many empty files. Treat them as scaffold names, not
  reliable implementation.
- Do not use EF migrations as the first source of truth. The SQL schema already
  exists and has been validated.
- Do not build full auth in this slice. A lightweight demo identity is enough
  for the first running vertical slice.
- Build the frontend against mock services first, because the immediate goal is
  a reliable presentation demo that can be wired to backend APIs afterward.
- Avoid broad CRUD work until the dashboard and simulation path are working.
- Keep the first demo deterministic so it can be presented reliably.
