# Authentication and Change Password Design

## Goal

Replace the demo-only sign-in path with a minimal production-style authentication flow and deliver the Change Password page defined by the HTML prototype.

## Backend

- `POST /api/auth/login` verifies an active, non-deleted user with bcrypt and returns a 15-minute JWT access token plus a random refresh token.
- Only a SHA-256 hash of the refresh token is stored in `operational.users`; the raw token is returned once to the client.
- JWT claims include user id, email, name, and role.
- `PUT /api/auth/change-password` requires JWT authentication, validates the current password, enforces the strong-password rule, updates the bcrypt hash at cost 12, and clears refresh-token fields.
- Login and password-change responses never expose password hashes.
- The default admin seed receives a real bcrypt hash for `Admin@2026!`.

## Frontend

- Login submits email/password to the real API and stores the returned session in local storage.
- API requests automatically include the access token.
- App startup restores a non-expired stored session.
- The Change Password page follows the HTML design: current password, new password, confirmation, live strength meter, validation messages, Cancel, and Confirm.
- A successful change clears the local session and returns the user to Login.
- API failures are shown in Vietnamese; no mock fallback is used for authentication.

## Security boundaries

This slice does not add password reset email, refresh-token rotation, multi-device session tables, or server-side access-token revocation. Clearing the single refresh-token hash revokes future refresh use; existing access tokens expire naturally after 15 minutes.

## Verification

Backend tests cover password policy, token creation, login/change-password contracts, and database-backed password replacement where the test database is available. Frontend tests cover validation, API submission, success logout, and error display. Both projects must build.
