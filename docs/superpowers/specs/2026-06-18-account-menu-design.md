# Account Menu Design

## Goal

Restore the header user icon behavior from the HTML prototype. Clicking the icon opens an account drop-card instead of navigating directly to the profile page.

## Behavior

- The user icon is a toggle button with an expanded state.
- The menu displays the current user's name, email, and role.
- The menu contains links to `/profile` and `/change-password`.
- The menu contains a logout button wired to the existing `onLogout` callback.
- Clicking the user icon again, selecting a menu link, pressing Escape, or clicking outside closes the menu.
- The prototype-only role switcher is intentionally omitted.

## Structure and styling

`Topbar` owns the open state and outside-click behavior because the trigger and menu form one interaction boundary. `AppShell` passes the existing logout callback to `Topbar`. Styling follows the prototype's `.account-menu`, `.menu-head`, and `.menu-item` dimensions and positioning.

## Verification

Component tests cover opening, closing, menu links, and logout. The full frontend test suite and TypeScript/Vite build are run afterward.
