# Account Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the header profile link with the HTML prototype's interactive account drop-card.

**Architecture:** Keep account-menu state and dismissal handling inside `Topbar`, where the trigger and popup live. Pass the existing logout callback from `AppShell`, and use React Router links for profile and password navigation.

**Tech Stack:** React 18, React Router, TypeScript, Vitest, Testing Library, CSS.

---

### Task 1: Add account-menu regression coverage

**Files:**
- Modify: `frontend/src/components/layout/Topbar.test.tsx`

- [ ] Add a test that clicks the account button and asserts the user details, profile link, password link, and logout button appear.
- [ ] Assert a second trigger click and an outside click close the menu.
- [ ] Assert logout invokes `onLogout`.
- [ ] Run `npm test -- src/components/layout/Topbar.test.tsx` and confirm failure because the current control is a direct link.

### Task 2: Implement the account drop-card

**Files:**
- Modify: `frontend/src/components/layout/Topbar.tsx`
- Modify: `frontend/src/components/layout/AppShell.tsx`
- Modify: `frontend/src/components/common/Icon.tsx`
- Modify: `frontend/src/styles/layout.css`

- [ ] Add `onLogout` to `TopbarProps` and pass it from `AppShell`.
- [ ] Replace the account `Link` with an accessible toggle button.
- [ ] Render profile, password, and logout menu items when expanded.
- [ ] Close on outside click, Escape, navigation, and logout.
- [ ] Add lock and logout icons used by the prototype menu.
- [ ] Add prototype-aligned account-menu styles.
- [ ] Run the focused test and confirm it passes.

### Task 3: Verify frontend behavior

**Files:**
- No additional files.

- [ ] Run `npm test` and confirm all tests pass.
- [ ] Run `npm run build` and confirm TypeScript and Vite build pass.
