# Authentication and Change Password Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add real JWT login and authenticated password change across the PORMS backend and frontend.

**Architecture:** The API owns password verification, bcrypt hashing, JWT issuance, and refresh-token hash persistence. The frontend stores the returned session, injects bearer authentication into API requests, and renders the prototype-aligned password form.

**Tech Stack:** ASP.NET Core, Npgsql, BCrypt.Net, JWT Bearer, React, TypeScript, Vitest, Testing Library.

---

### Task 1: Backend authentication core

**Files:**
- Create: `backend/PORMS.API/Configuration/JwtOptions.cs`
- Create: `backend/PORMS.API/Contracts/AuthContracts.cs`
- Modify: `backend/PORMS.Infrastructure/Repositories/UserRepository.cs`
- Modify: `backend/PORMS.Application/Services/Auth/IAuthService.cs`
- Modify: `backend/PORMS.Application/Services/Auth/AuthService.cs`
- Modify: `backend/PORMS.API/Controllers/AuthController.cs`
- Modify: `backend/PORMS.API/Program.cs`
- Modify: `backend/PORMS.API/PORMS.API.csproj`
- Modify: `backend/PORMS.API/appsettings.json`
- Test: `backend/PORMS.Tests/Unit/AuthServiceTests.cs`

- [ ] Write failing tests for password strength and incorrect current password.
- [ ] Run the focused tests and verify they fail because auth behavior is absent.
- [ ] Implement bcrypt verification/hash, JWT generation, refresh-token hashing, and repository updates.
- [ ] Add authenticated login and change-password endpoints.
- [ ] Run focused backend tests and verify they pass.

### Task 2: Seed and frontend session

**Files:**
- Modify: `scripts/porms_migration.sql`
- Create: `frontend/src/types/auth.ts`
- Replace: `frontend/src/services/authService.ts`
- Modify: `frontend/src/services/api.ts`
- Modify: `frontend/src/App.tsx`
- Modify: `frontend/src/router/index.tsx`
- Modify: `frontend/src/pages/LoginPage.tsx`
- Test: `frontend/src/services/authService.test.ts`

- [ ] Replace the seed placeholder with a real cost-12 bcrypt hash.
- [ ] Write failing frontend tests for login request and session persistence.
- [ ] Implement real login, local session restore, bearer headers, and logout cleanup.
- [ ] Run focused frontend tests and verify they pass.

### Task 3: Change Password page

**Files:**
- Replace: `frontend/src/pages/ChangePasswordPage.tsx`
- Modify: `frontend/src/styles/pages.css`
- Modify: `frontend/src/router/index.tsx`
- Test: `frontend/src/pages/ChangePasswordPage.test.tsx`

- [ ] Write failing tests for strength feedback, mismatched confirmation, API submission, errors, and successful logout.
- [ ] Run the focused tests and verify expected failures.
- [ ] Implement the prototype-aligned form and API flow.
- [ ] Run focused tests, all frontend tests, backend tests, and both builds.
