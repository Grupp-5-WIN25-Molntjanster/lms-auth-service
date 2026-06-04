# LMS Auth Service

Authentication and authorization microservice for the LMS platform. Handles user
registration, email verification, login, JWT issuing, refresh-token rotation, logout,
and password changes. Built with ASP.NET Core on **.NET 10** following Clean Architecture.

Email verification is **delegated to a separate Verification service over HTTP** — Auth
no longer owns the verification-code lifecycle (see **Verification service integration**
below).

---

## Live deployment

The service is deployed to **Azure App Service**:

- Base URL → `https://lmsauthapi20260522165735.azurewebsites.net`
- Health check → `https://lmsauthapi20260522165735.azurewebsites.net/health`
- Interactive API docs (Scalar) → `https://lmsauthapi20260522165735.azurewebsites.net/scalar/v1`
- OpenAPI document → `https://lmsauthapi20260522165735.azurewebsites.net/openapi/v1.json`

---

## Tech stack

- **.NET 10** / ASP.NET Core Web API
- **Entity Framework Core 10** + SQL Server (schema-isolated under `Auth`)
- **JWT Bearer** authentication (HMAC-SHA256)
- **BCrypt** password hashing (`BCrypt.Net-Next`)
- **HTTP client** (`IHttpClientFactory`) to the **Verification service** for sending and
  validating email-verification codes
- **Serilog** structured logging
- **Scalar** for interactive API docs (OpenAPI)
- **xUnit + Moq + FluentAssertions** for tests

---

## Architecture

The solution is split into four layers plus two test projects, with dependencies
pointing inward (`Api → Application → Domain`, `Infrastructure → Application/Domain`).

```
Lms.Auth/
├── Lms.Auth.Api              # Controllers, Program.cs, DI wiring, config
├── Lms.Auth.Application       # AuthService, DTOs, application interfaces
├── Lms.Auth.Domain            # Entities (User, RefreshToken), value objects, enums
├── Lms.Auth.Infrastructure    # EF Core, repositories, JWT, hashing, Verification client
├── Lms.Auth.UnitTests         # AuthService, JWT, PasswordHasher tests
└── Lms.Auth.IntegrationTests  # AuthController end-to-end tests (in-memory DB)
```

- **Domain** — `User` and `RefreshToken` are rich entities (private setters, behavior
  methods like `ConfirmEmail()`, `RecordLogin()`, `UpdatePassword()`, `Revoke()`).
  `BaseEntity` supplies a `Guid Id` and audit timestamps. Roles are `Student`,
  `Instructor`, `Admin`.
- **Application** — `AuthService` orchestrates the use cases and depends only on
  interfaces (`IUserRepository`, `IRefreshTokenRepository`, `IPasswordHasher`,
  `IJwtTokenGenerator`, `IApplicationDbContext`, `IVerificationClient`).
- **Infrastructure** — `AuthDbContext`, repositories, `JwtTokenGenerator`,
  `PasswordHasher`, and the Verification HTTP clients (`VerificationClient` /
  `NoOpVerificationClient`). All wired via `AddInfrastructure(configuration)`.
- **Api** — thin `AuthController`; JWT validation and the migration-on-startup step
  live in `Program.cs`.

---

## Verification service integration

Auth does **not** generate, store, or validate verification codes. That entire lifecycle
(generate → store → dispatch → validate) is owned by a separate **Verification service**.
Auth only:

1. **Triggers a send** on registration / resend → `POST {VerificationService}/api/Verification/send`
   with body `{ "email": "..." }`.
2. **Asks whether a submitted code is valid** on verify-email →
   `POST {VerificationService}/api/Verification/validate` with body
   `{ "email": "...", "code": 123456 }` (the code is sent as a **number**). Success is
   determined by the HTTP status code (200 = valid, 400 = invalid); there is no `valid`
   field in the response body.

This is abstracted behind `IVerificationClient`, with two implementations selected at
startup based on configuration:

| Implementation | When it is used | Behavior |
|---|---|---|
| `VerificationClient` | `ServiceUrls:VerificationService` **is set** | Real `HttpClient` calls to the Verification service (10s timeout). |
| `NoOpVerificationClient` | `ServiceUrls:VerificationService` **is empty/missing** | Logs a warning, skips the send, and **auto-accepts every code** so the app runs standalone. |

> On registration, the send call is wrapped in a `try/catch`: a transient Verification
> outage does **not** lose the new account — the user is persisted and can call
> `/resend-verification` later.

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB, Express, or a full instance)
- A running **Verification service** *(optional — see note below)*

### Run locally

```bash
# from the Lms.Auth folder
dotnet restore
dotnet run --project Lms.Auth.Api
```

The API listens on `http://localhost:5068` (and `https://localhost:7234` with the
`https` profile). On startup it automatically applies EF Core migrations against the
configured database (skipped under the `Testing` environment).

Once running:

- API root health string → `http://localhost:5068/`
- Health check → `http://localhost:5068/health`
- Interactive docs (Scalar) → `http://localhost:5068/scalar/v1`
- OpenAPI document → `http://localhost:5068/openapi/v1.json`

> **Verification note:** the committed `appsettings.json` already points
> `ServiceUrls:VerificationService` at the deployed Azure Verification service, so by
> default registration/verify-email will call that real service even when running
> locally. To run Auth completely standalone, clear `ServiceUrls:VerificationService`
> (e.g. via user secrets / environment) — `NoOpVerificationClient` then takes over and
> **any code passed to `verify-email` will be accepted**.

---

## Configuration

Settings are read from `appsettings.json` / `appsettings.Development.json` and can be
overridden by environment variables or user secrets.

| Section | Key | Description |
|---|---|---|
| `ConnectionStrings` | `AuthDb` | SQL Server connection string |
| `Jwt` | `Secret` | Signing key. **Required at startup** |
| `Jwt` | `Issuer` | Token issuer. **Required at startup** |
| `Jwt` | `Audience` | Token audience. **Required at startup** |
| `Jwt` | `AccessTokenExpirationMinutes` | Access-token lifetime (committed value: **1440**; class default 15) |
| `Jwt` | `RefreshTokenExpirationDays` | Refresh-token lifetime (default 7) |
| `ServiceUrls` | `VerificationService` | Base URL of the Verification service. If empty, the no-op client is used |

`Program.cs` throws on startup if `Jwt:Secret`, `Jwt:Issuer`, or `Jwt:Audience` are
missing, so the app fails fast on misconfiguration.

> The committed `appsettings.json` ships `Jwt:Secret` and `Jwt:Issuer` **empty** on
> purpose — supply them via user secrets / environment variables before running.

---

## API reference

Base route: `/api/auth`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/register` | Anonymous | Create an account; triggers a verification send |
| `POST` | `/api/auth/verify-email` | Anonymous | Confirm email with the code (validated by Verification service) |
| `POST` | `/api/auth/resend-verification` | Anonymous | Re-trigger a verification send |
| `POST` | `/api/auth/login` | Anonymous | Authenticate; returns access + refresh tokens |
| `POST` | `/api/auth/refresh` | Anonymous | Exchange a refresh token for new tokens |
| `POST` | `/api/auth/logout` | Bearer | Revoke a refresh token |
| `POST` | `/api/auth/change-password` | Bearer | Change the current user's password |
| `GET`  | `/api/auth/validate` | Bearer | Validate the current token, return the profile |

### Example: register

```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "student@example.com",
  "password": "Passw0rd!",
  "firstName": "Ada",
  "lastName": "Lovelace",
  "role": "Student"
}
```

Returns `201 Created` with a `TokenResponse` where `requiresEmailVerification` is `true`
and tokens are empty — the user must verify their email before logging in. Returns
`409 Conflict` (`email_taken`) if the email already exists.

> **Getting the verification code during testing.** Auth no longer stores codes, so the
> code does **not** live in the Auth database. Where you read it depends on configuration:
>
> - **`ServiceUrls:VerificationService` set** → the code is owned by the Verification
>   service; obtain it from that service / its data store, then submit it to
>   `verify-email`. Passing a wrong code returns `400 Bad Request`
>   (`verification_failed`).
> - **`ServiceUrls:VerificationService` empty** → `NoOpVerificationClient` is active and
>   **any** code is accepted, so `verify-email` succeeds with anything.

### Example: verify-email

```http
POST /api/auth/verify-email
Content-Type: application/json

{
  "email": "student@example.com",
  "code": "123456"
}
```

Returns `200 OK` on success. The `code` is sent to Auth as a string but forwarded to the
Verification service as a number, so it must be numeric.

### Example: login

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "student@example.com",
  "password": "Passw0rd!"
}
```

```json
{
  "accessToken": "eyJ...",
  "refreshToken": "base64...",
  "expiresAt": "2026-05-23T12:15:00Z",
  "requiresEmailVerification": false,
  "user": {
    "id": "…",
    "email": "student@example.com",
    "firstName": "Ada",
    "lastName": "Lovelace",
    "role": "Student",
    "isActive": true,
    "emailConfirmed": true,
    "createdAt": "2026-05-23T11:00:00Z",
    "lastLoginAt": "2026-05-23T12:00:00Z"
  }
}
```

Returns `401 Unauthorized` (`invalid_credentials`) on a bad email/password or an inactive
account. If the credentials are correct but the **email is not yet confirmed**, login
returns `200 OK` with empty tokens and `requiresEmailVerification: true` instead of an
error.

### Example: change-password

```http
POST /api/auth/change-password
Authorization: Bearer eyJ...
Content-Type: application/json

{
  "currentPassword": "Passw0rd!",
  "newPassword": "N3wPassw0rd!",
  "confirmNewPassword": "N3wPassw0rd!"
}
```

Returns `200 OK` with `{ "message": "..." }` on success, or `400 Bad Request`
(`password_change_failed`) with a `details` array on failure. The new password is
validated against `PasswordRequirements`: **8–128 characters**, with at least one
uppercase letter, one lowercase letter, one digit, and one special character; it must
match the confirmation and differ from the current password.

### Authenticating requests

Send the access token as a bearer header:

```http
Authorization: Bearer eyJ...
```

Access tokens are signed with HMAC-SHA256 and carry `sub`, `email`, `name`, `role`,
`firstName`, and `lastName` claims. The token is validated with `NameClaimType = "name"`
and `RoleClaimType = "role"`; `sub` maps to `ClaimTypes.NameIdentifier`, which the
controller reads for the current user id. Lifetimes follow configuration
(committed: **access 1440 min**, **refresh 7 days**).

### Refresh and logout body shape

`refresh` and `logout` currently bind the refresh token as a **raw JSON string**, not an
object — so the request body is a quoted string, e.g.:

```http
POST /api/auth/refresh
Content-Type: application/json

"base64-refresh-token-value"
```

(A `RefreshRequest` DTO exists in the codebase but isn't wired up yet; switching to it
would make the body `{ "refreshToken": "..." }` and read better in the Scalar docs.)

`logout` also requires a valid access token in the `Authorization` header *and* the
refresh token in the body.

---

## Auth flow

```
register ──► (Verification service asked to send a code over HTTP)
            │
            ▼
       verify-email ──► (code checked against Verification service) ──► EmailConfirmed = true
            │
            ▼
         login ──► access token + refresh token
            │
   ┌────────┼─────────────────┐
   ▼        ▼                  ▼
 refresh  change-password   validate
(rotate)  (update hash)     (read profile)
   │
   ▼
 logout (revoke refresh token)
```

Refresh tokens are stored in the database so they can be revoked; on refresh, the old
token is revoked and a new one is issued (rotation).

---

## Testing

```bash
# from the Lms.Auth folder
dotnet test
```

- **Lms.Auth.UnitTests** — `AuthService`, `JwtTokenGenerator`, and `PasswordHasher`.
- **Lms.Auth.IntegrationTests** — `AuthController` against an in-memory EF Core provider
  via `Microsoft.AspNetCore.Mvc.Testing`. `TestingWebAppFactory` swaps the SQL Server
  `AuthDbContext` for an InMemory one. Because no `ServiceUrls:VerificationService` is
  configured in the test host, the `NoOpVerificationClient` is used, so verification
  auto-accepts during integration tests.

---

## Database

EF Core targets SQL Server. All tables live under the `Auth` schema (`Auth.Users`,
`Auth.RefreshTokens`) so the service can share a physical database with other LMS
services without table-name collisions. `Email` is unique-indexed; refresh tokens
cascade-delete with their user.


Migrations are applied automatically on application startup (`Database.Migrate()`),
except under the `Testing` environment.

