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
