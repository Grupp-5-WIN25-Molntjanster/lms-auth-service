# LMS Auth Service

Authentication and authorization microservice for the LMS platform. Handles user
registration, email verification, login, JWT issuing, refresh-token rotation, and
logout. Built with ASP.NET Core on **.NET 10** following Clean Architecture.

---

## Live deployment

The service is deployed to **Azure App Service**:

- Base URL → `https://lmsauthapi20260522165735.azurewebsites.net`
- Health check → `https://lmsauthapi20260522165735.azurewebsites.net/health`
- Interactive API docs (Scalar) → `https://lmsauthapi20260522165735.azurewebsites.net/scalar/v1`
- OpenAPI document → `https://lmsauthapi20260522165735.azurewebsites.net/openapi/v1.json`

> **Test against one environment at a time.** A user registered locally does not exist
> in the Azure database (and vice versa). When verifying email, use the verification
> code that belongs to *that* environment's database, and call `register` and
> `verify-email` on the same host.

---

## Tech stack

- **.NET 10** / ASP.NET Core Web API
- **Entity Framework Core 10** + SQL Server (schema-isolated under `Auth`)
- **JWT Bearer** authentication (HMAC-SHA256)
- **BCrypt** password hashing (`BCrypt.Net-Next`)
- **Azure Service Bus** for publishing verification-email messages
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
├── Lms.Auth.Infrastructure    # EF Core, repositories, JWT, hashing, Service Bus
├── Lms.Auth.UnitTests         # AuthService, JWT, PasswordHasher tests
└── Lms.Auth.IntegrationTests  # AuthController end-to-end tests (in-memory DB)
```

- **Domain** — `User` and `RefreshToken` are rich entities (private setters, behavior
  methods like `ConfirmEmail()`, `RecordLogin()`, `Revoke()`). `BaseEntity` supplies a
  `Guid Id` and audit timestamps. Roles are `Student`, `Instructor`, `Admin`.
- **Application** — `AuthService` orchestrates the use cases and depends only on
  interfaces (`IUserRepository`, `IRefreshTokenRepository`, `IPasswordHasher`,
  `IJwtTokenGenerator`, `IApplicationDbContext`, `IServiceBusPublisher`).
- **Infrastructure** — `AuthDbContext`, repositories, `JwtTokenGenerator`,
  `PasswordHasher`, and `ServiceBusPublisher`. All wired via
  `AddInfrastructure(configuration)`.
- **Api** — thin `AuthController`; JWT validation and the migration-on-startup step
  live in `Program.cs`.

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB, Express, or a full instance)
- An Azure Service Bus namespace + queue *(optional — see note below)*

### Run locally

```bash
# from the Lms.Auth folder
dotnet restore
dotnet run --project Lms.Auth.Api
```

The API listens on `http://localhost:5068` (and `https://localhost:7234` with the
`https` profile). On startup it automatically applies EF Core migrations against the
configured database.

Once running:

- API root health string → `http://localhost:5068/`
- Health check → `http://localhost:5068/health`
- Interactive docs (Scalar) → `http://localhost:5068/scalar/v1`
- OpenAPI document → `http://localhost:5068/openapi/v1.json`

> **Service Bus note:** the actual `SendMessageAsync` call in `ServiceBusPublisher` is
> currently commented out, so registration/verification flows run end-to-end without a
> live Service Bus connection. The verification code is generated and persisted on the
> user record regardless. Re-enable the send when the Email Service is wired up.

---

## Configuration

Settings are read from `appsettings.json` / `appsettings.Development.json` and can be
overridden by environment variables or user secrets.

| Section | Key | Description |
|---|---|---|
| `ConnectionStrings` | `AuthDb` | SQL Server connection string |
| `Jwt` | `Secret` | Signing key (Base64). **Required at startup** |
| `Jwt` | `Issuer` | Token issuer. **Required at startup** |
| `Jwt` | `Audience` | Token audience. **Required at startup** |
| `Jwt` | `AccessTokenExpirationMinutes` | Access-token lifetime (default 15) |
| `Jwt` | `RefreshTokenExpirationDays` | Refresh-token lifetime (default 7) |
| `AzureServiceBus` | `ConnectionString` | Service Bus connection string |
| `AzureServiceBus` | `VerifyQueueName` | Queue name (default `verify-queue`) |

`Program.cs` throws on startup if `Jwt:Secret`, `Jwt:Issuer`, or `Jwt:Audience` are
missing, so the app fails fast on misconfiguration.


## API reference

Base route: `/api/auth`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/register` | Anonymous | Create an account; sends a verification code |
| `POST` | `/api/auth/verify-email` | Anonymous | Confirm email with the 6-digit code |
| `POST` | `/api/auth/resend-verification` | Anonymous | Re-send a verification code |
| `POST` | `/api/auth/login` | Anonymous | Authenticate; returns access + refresh tokens |
| `POST` | `/api/auth/refresh` | Anonymous | Exchange a refresh token for new tokens |
| `POST` | `/api/auth/logout` | Bearer | Revoke a refresh token |
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

Returns `201 Created` with a `TokenResponse` where `requiresEmailVerification` is
`true` and tokens are empty — the user must verify their email before logging in.
Returns `409 Conflict` (`email_taken`) if the email already exists.

> **Getting the verification code during testing.** A 6-digit code is generated and
> stored on the user row (`Auth.Users.VerificationCode`, valid for 1 hour). The actual
> email send is delegated to the Email Service via Service Bus, so for manual testing
> read the code straight from the database against the environment you registered on:
>
> ```sql
> SELECT Email, VerificationCode, VerificationCodeExpiresAt, EmailConfirmed
> FROM Auth.Users WHERE Email = 'you@example.com';
> ```
>
> Passing a guessed code returns `400 Bad Request` (`verification_failed`).

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
  "user": {
    "id": "…",
    "email": "student@example.com",
    "firstName": "Ada",
    "lastName": "Lovelace",
    "role": "Student",
    "isActive": true
  }
}
```

Returns `401 Unauthorized` (`invalid_credentials`) on a bad email/password or an
inactive account.

### Authenticating requests

Send the access token as a bearer header:

```http
Authorization: Bearer eyJ...
```

Access tokens are signed with HMAC-SHA256 and carry `sub`, `email`, `name`, `role`,
`firstName`, and `lastName` claims. Default lifetimes: **access 15 min**,
**refresh 7 days**.

### Refresh and logout body shape

`refresh` and `logout` currently bind the refresh token as a **raw JSON string**, not
an object — so the request body is a quoted string, e.g.:

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
register ──► (verification code generated + published)
            │
            ▼
       verify-email ──► EmailConfirmed = true
            │
            ▼
         login ──► access token + refresh token
            │
   ┌────────┴─────────┐
   ▼                  ▼
 refresh           validate
(rotate tokens)   (read profile)
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
- **Lms.Auth.IntegrationTests** — `AuthController` against an in-memory EF Core
  provider via `Microsoft.AspNetCore.Mvc.Testing`.

---

## Database

EF Core targets SQL Server. All tables live under the `Auth` schema (`Auth.Users`,
`Auth.RefreshTokens`) so the service can share a physical database with other LMS
services without table-name collisions. `Email` is unique-indexed; refresh tokens
cascade-delete with their user.

Migrations are applied automatically on application startup (`Database.Migrate()`).

---

## Notes for the team (Grupp 5)

- The service is live on Azure App Service (see **Live deployment** above) with Scalar
  docs exposed publicly at `/scalar/v1`.
- This service publishes an `EmailVerification` message to the Service Bus queue that
  the **Email Service** consumes. The message shape is `VerificationMessage`
  (`userId`, `email`, `verificationCode`, `sentAt`, `messageType`).
- The Service Bus send is **currently commented out** in
  `ServiceBusPublisher.PublishVerificationEmailAsync` — codes are generated and stored
  but not yet delivered by email. Uncomment the send once the queue and credentials are
  provisioned and the Email Service is consuming `verify-queue`.
- When integrating behind the API Gateway, the gateway should forward the
  `Authorization` header unchanged so downstream `[Authorize]` endpoints work.
