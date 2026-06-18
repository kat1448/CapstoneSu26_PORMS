# PORMS Dashboard Design Fidelity

## Objective

Align the React application shell and Dashboard with
`D:\14.Business\007.fpt_support\001.design\design.html` while preserving the
current API integration, routing, simulation behavior, and backend contracts.

The target is visual and structural fidelity to the prototype, not a new design
direction.

## Scope

### In Scope

- Rebuild the shared sidebar to match the prototype's fixed 224px navy
  navigation.
- Rebuild the shared topbar to match the prototype's 58px header.
- Reorder and resize Dashboard sections to match the prototype.
- Restore the Dashboard zone-status panel.
- Move current-weather presentation into the Dashboard right column.
- Remove the recent operation-log card from Dashboard while retaining the
  dedicated Operation Log route.
- Match prototype colors, typography, spacing, borders, radii, badges, and
  responsive behavior.
- Correct visibly corrupted Vietnamese strings in the shell and Dashboard
  components touched by this work.

### Out of Scope

- Backend API or database contract changes.
- Authentication and role enforcement changes.
- Redesigning non-Dashboard page content.
- Replacing the current router or service layer.
- Pixel-perfect conversion of modal and form screens outside Dashboard.

## Visual Source of Truth

Use `001.design/design.html` as the visual source of truth, especially:

- root design tokens and responsive rules;
- `.sidebar`, `.topbar`, `.content`, and `.card`;
- `.risk-hero`, `.mode-panel`, chart, zone, alert, and weather styles;
- `renderDashboard()` markup and content ordering.

When the prototype and current React behavior differ, preserve React behavior
but use the prototype's layout and visual treatment.

## Shared Application Shell

### Sidebar

- Fixed to the left edge and full viewport height.
- Width: 224px on desktop.
- Navy background using the prototype palette.
- Brand area, grouped navigation labels, icons, active state, and alert count
  follow the prototype.
- Navigation labels use Vietnamese names from the prototype.
- On screens below 780px, the sidebar becomes an off-canvas panel controlled by
  a menu button in the topbar.

### Topbar

- Sticky at the top of the content area.
- Height: 58px.
- Left side contains the current page title and port breadcrumb.
- Right side contains the live clock, alert button/count, refresh action, and
  account control.
- Mobile rules hide secondary information and nonessential actions as specified
  by the prototype.

### Content Area

- Desktop content padding: 20px.
- Mobile content padding: 13px.
- Cards use the prototype border, subtle shadow, and 10px radius.

## Dashboard Structure

The page heading contains:

- title: `Trung tâm điều hành`;
- subtitle describing real-time operational monitoring;
- existing demo simulation action.

The primary Dashboard grid uses:

```text
Left column:  1.25fr
Right column: 0.75fr
Gap:          14px
```

### Left Column

1. Risk hero and operation mode cards in a two-column row.
2. Risk trend chart.
3. Zone status card.

### Right Column

1. Current weather card.
2. Active alerts card.

The recent operation-log card is removed from Dashboard. Operation events remain
available through the existing `/operation-log` route.

## Component Behavior

### Risk Hero

- Preserve the current risk value from the API.
- Use the prototype gradient, typography, score mapping, progress percentages,
  and decorative ring.
- Display the dominant weather reason using current weather data where
  available.

### Operation Mode

- Preserve the current operation mode from the API.
- Use the prototype orb, description, status badge, and card alignment.
- Do not add mode override behavior during this visual-alignment task.

### Risk Trend

- Preserve current risk-trend API data.
- Match the compact prototype heading, status badge, chart height, axes, line,
  and point styling.

### Zone Status

- Load zones for the current Dashboard port through the existing port service.
- Show a compact grid of monitored zones with name, type/status, icon, and risk
  badge.
- Link to the existing port-detail route.
- Empty and loading states remain compact and do not disturb the Dashboard grid.

### Weather

- Preserve current weather API data.
- Use the compact weather summary presentation from the prototype.
- Show wind speed, Beaufort level, rain, visibility, temperature, and humidity
  when those values exist in the current frontend model.
- Values unavailable in the current contract are omitted rather than invented.

### Alerts

- Preserve current alert API data.
- Show unread active alerts first, limited to four rows.
- Match prototype icon, title, zone/message preview, and relative-time layout.
- Link to the existing Alerts route.

## Data Flow

```text
DashboardPage
  -> existing dashboard, weather, alert, port, and zone services
  -> existing API-first behavior and configured mock fallback
  -> presentation components styled to match design.html
```

No backend changes are required for this design.

## Text Encoding

All shell and Dashboard source files modified by this work must contain valid
UTF-8 Vietnamese text. Mojibake such as `Cáº£ng` or `Äang` is not acceptable.

This cleanup is limited to files touched by the shell and Dashboard alignment.

## Responsive Behavior

- At widths below 1100px, the Dashboard main columns stack.
- At widths below 780px, the sidebar becomes off-canvas, Dashboard subgrids
  become single-column, and topbar actions collapse according to the prototype.
- No horizontal page overflow is allowed at common mobile widths.

## Verification

- Frontend TypeScript production build succeeds.
- Dashboard loads using API mode with mock fallback disabled.
- Sidebar, topbar, risk, mode, trend, zone, weather, and alert sections are
  visually present in the approved order.
- Existing routes remain navigable.
- Demo simulation still refreshes Dashboard risk, weather, and alerts.
- Desktop and mobile layouts are inspected against `design.html`.
- Touched shell and Dashboard files contain no visible mojibake.

