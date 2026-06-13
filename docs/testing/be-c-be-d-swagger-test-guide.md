# BE-C / BE-D Swagger Test Guide

This guide uses the seeded Da Nang Tien Sa port:

```text
portId = 09674fa3-1136-490d-8a0f-a980f0065e05
```

## 1. Start Docker and API

```powershell
cd D:\Training2023\project\CapstoneSu26_PORMS\infra
docker compose --env-file ../.env up -d postgres

cd ..
dotnet run --project backend/PORMS.API/PORMS.API.csproj --urls http://localhost:5099
```

Open Swagger:

```text
http://localhost:5099/swagger
```

## 2. Check SOP Rules

Swagger endpoint:

```text
GET /api/sop-rules
```

Use:

```text
pageSize = 50
```

Expected result:

```text
200 OK
data contains seeded SOP rules
```

Optional export test:

```text
GET /api/sop-rules/export
```

Use:

```text
activeOnly = true
```

Expected:

```text
200 OK
returns active SOP rules as JSON
```

Optional import test:

```text
POST /api/sop-rules/import
```

Body can be a single object or an array:

```json
[
  {
    "ruleName": "TMP-IMPORT-TEST: temporary imported rule",
    "triggerRiskLevel": "LOW",
    "appliesToZoneType": null,
    "actionType": "CUSTOM",
    "actionDescription": "Temporary imported SOP rule for API smoke test.",
    "targetOperationMode": null,
    "executionOrder": 250,
    "alertMessage": "Temporary imported SOP alert.",
    "alertSeverity": "INFO",
    "updatedByUserId": null
  }
]
```

Expected:

```text
201 Created
imported = 1
```

After testing import, copy the returned rule `id` and disable it:

```text
DELETE /api/sop-rules/{id}
```

## 3. Check Current Operation Mode

Dashboard status endpoint:

```text
GET /api/ports/{portId}/status
```

Use:

```text
portId = 09674fa3-1136-490d-8a0f-a980f0065e05
```

Expected fields:

```text
currentMode
currentRiskLevel
latestWeather
latestRisk
zones
unreadAlertCount
isStale
```

Swagger endpoint:

```text
GET /api/ports/{portId}/mode/current
```

Use:

```text
portId = 09674fa3-1136-490d-8a0f-a980f0065e05
```

Expected fields:

```text
currentMode
currentRiskLevel
lastChangedAt
```

## 4. Reset Risk to LOW

Swagger endpoint:

```text
POST /api/weather/manual-input
```

Body:

```json
{
  "portId": "09674fa3-1136-490d-8a0f-a980f0065e05",
  "windSpeedMs": 3,
  "rainfall1hMm": 0,
  "visibilityKm": 12,
  "temperatureC": 29,
  "humidityPct": 75,
  "observedAt": "2026-06-11T13:00:00Z",
  "notes": "Reset LOW before HIGH SOP test"
}
```

Expected result:

```text
201 Created
final risk becomes LOW
```

## 5. Trigger HIGH Risk

Swagger endpoint:

```text
POST /api/weather/manual-input
```

Body:

```json
{
  "portId": "09674fa3-1136-490d-8a0f-a980f0065e05",
  "windSpeedMs": 19,
  "rainfall1hMm": 20,
  "visibilityKm": 8,
  "temperatureC": 30,
  "humidityPct": 80,
  "observedAt": "2026-06-11T13:05:00Z",
  "notes": "HIGH wind test for SOP engine"
}
```

Expected result:

```text
201 Created
beaufortNumber = 8
Risk Engine evaluates HIGH
SOP Engine is triggered
```

## 6. Verify SOP Chain

Check SOP event:

```text
GET /api/operation-events
```

Use:

```text
portId = 09674fa3-1136-490d-8a0f-a980f0065e05
eventType = SOP_TRIGGERED
pageSize = 5
```

Expected summary:

```text
SOP engine handled HIGH: 7 executed, 0 skipped, 0 failed.
```

Check SOP execution details:

```text
GET /api/sop-rules/executions
```

Use:

```text
portId = 09674fa3-1136-490d-8a0f-a980f0065e05
pageSize = 20
```

Expected:

```text
data contains 7 HIGH executions after a LOW -> HIGH risk change
executionResult.status = EXECUTED
```

Check alerts:

```text
GET /api/alerts
```

Use:

```text
portId = 09674fa3-1136-490d-8a0f-a980f0065e05
pageSize = 10
```

Expected:

```text
alerts are created by SOP rules
```

Check alert stats:

```text
GET /api/alerts/stats
```

Use:

```text
portId = 09674fa3-1136-490d-8a0f-a980f0065e05
date = 2026-06-11
```

Expected:

```text
totalToday
unread
criticalToday
averageResponseMinutes
```

Check tasks:

```text
GET /api/tasks
```

Use:

```text
portId = 09674fa3-1136-490d-8a0f-a980f0065e05
pageSize = 10
```

Expected:

```text
tasks are created by SOP rules
```

Check mode:

```text
GET /api/ports/{portId}/mode/current
```

Expected after HIGH test:

```text
currentMode = STOP
currentRiskLevel = HIGH
```

## 7. Mark Alert as Read

First call:

```text
GET /api/alerts
```

Copy an alert `id` from the response. Do not use `portId` here.

Example:

```json
{
  "id": "copy-this-alert-id",
  "portId": "09674fa3-1136-490d-8a0f-a980f0065e05"
}
```

Then call:

```text
PATCH /api/alerts/{id}/read
```

Body:

```json
{
  "userId": null
}
```

Expected:

```text
200 OK
response contains readAt
```

Then check:

```text
GET /api/alerts/unread-count?portId=09674fa3-1136-490d-8a0f-a980f0065e05
```

## Notes

If you trigger LOW after HIGH, `currentRiskLevel` becomes `LOW`, but `currentMode` may remain `STOP`.
This is intentional for safety: recovery creates an INFO alert and task, but the port should not automatically resume operation without manual review.

If you trigger HIGH while the port is already `STOP`, `SOP_TRIGGERED` still creates alerts and tasks, but `modeChanges` can be `0` because there is no new mode transition to apply.

## 8. Test Simulation Mode

Simulation is for demo/training. It does not forecast the future. It replays weather snapshots through the real pipeline:

```text
WeatherReading -> RiskEngine -> SopEngine -> Alert/Task/Mode logs
```

Use this demo dataset:

```text
demo/storm_scenario_danang_oct2023.json
```

Swagger endpoint:

```text
POST /api/simulation/start
```

Body:

```json
{
  "portId": "09674fa3-1136-490d-8a0f-a980f0065e05",
  "scenarioName": "Quick simulation smoke test",
  "speedMultiplier": 100,
  "startedByUserId": null,
  "weatherSnapshots": [
    { "windSpeedMs": 3, "rainfall1hMm": 0, "visibilityKm": 12, "temperatureC": 29, "humidityPct": 75, "observedAt": "2026-06-12T00:00:00Z" },
    { "windSpeedMs": 11, "rainfall1hMm": 12, "visibilityKm": 8, "temperatureC": 29, "humidityPct": 78, "observedAt": "2026-06-12T00:15:00Z" },
    { "windSpeedMs": 19, "rainfall1hMm": 25, "visibilityKm": 4, "temperatureC": 28, "humidityPct": 84, "observedAt": "2026-06-12T00:30:00Z" },
    { "windSpeedMs": 26, "rainfall1hMm": 55, "visibilityKm": 0.8, "temperatureC": 27, "humidityPct": 90, "observedAt": "2026-06-12T00:45:00Z" },
    { "windSpeedMs": 8, "rainfall1hMm": 4, "visibilityKm": 11, "temperatureC": 29, "humidityPct": 76, "observedAt": "2026-06-12T01:00:00Z" }
  ]
}
```

Expected:

```text
201 Created
response contains id
status = RUNNING
```

Copy the returned `id`, then poll:

```text
GET /api/simulation/status?sessionId={id}
```

Expected while running:

```text
completedSnapshots increases
percentComplete increases
currentWeather shows the simulated snapshot
```

After it completes:

```text
GET /api/simulation/{id}/results
```

Expected:

```text
status = COMPLETED
weatherReadings >= 5
riskAssessments >= 5
sopExecutions / alertsGenerated / tasksGenerated are created when risk changes match SOP rules
peakRiskLevel should reach CRITICAL for the smoke test
```

To stop a running simulation:

```text
POST /api/simulation/stop
```

Body:

```json
{
  "sessionId": "copy-session-id-here"
}
```

Expected:

```text
200 OK
status = CANCELLED
```

## 9. Test Port Decision Support

This endpoint gives the operator-facing answer: whether weather-sensitive operations should continue, be restricted, or stop.

Swagger endpoint:

```text
GET /api/ports/{portId}/decision-support
```

Use:

```text
portId = 09674fa3-1136-490d-8a0f-a980f0065e05
```

Expected fields:

```text
recommendationCode
recommendationText
canHandleContainers
canAcceptVesselEntry
decisionReasons
latestWeather
latestRisk
isWeatherDataStale
marineDataCoverage
activeSopRecommendations
```

Example interpretation:

```text
OPERATE_NORMALLY       = weather-sensitive operations can continue under monitoring
OPERATE_WITH_CAUTION   = operations can continue but operator should monitor conditions
RESTRICT_OPERATIONS    = container handling/vessel entry should be restricted
STOP_OPERATIONS        = do not handle containers or accept vessel entry until reviewed
VERIFY_WEATHER_DATA    = weather data is stale or missing, verify conditions first
```

Note:

```text
If risk returns to LOW but currentMode is still STOP, decision-support still returns STOP_OPERATIONS.
This is intentional: the system does not automatically resume real operations after a risky condition.
Company Admin should review conditions and use manual mode override if appropriate.
```
