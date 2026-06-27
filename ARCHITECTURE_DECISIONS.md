# Architecture Decisions — MyMoney

This document is the architectural constitution for the MyMoney API. It records every significant architectural decision, the reason it was made, what patterns are accepted, what patterns are rejected, and the rules that must never be violated. Refer to `DEVELOPMENT_GUIDE.md` for implementation mechanics.

---

## 0. Remediation Addendum — 2026-06-28

The enterprise audit (`ENTERPRISE_BACKEND_AUDIT.md`) drove a set of changes that supersede or extend the decisions below. Where this addendum conflicts with a later section, the addendum wins.

- **Secrets management.** Secrets (JWT signing key, SMTP credentials, production connection strings) **must not** live in `appsettings.json`. They come from .NET user-secrets in development and environment variables (`Jwt__SecretKey`, `Smtp__Password`, `ConnectionStrings__SqlConnection`, …) in production. Startup fails fast if the JWT key is missing or < 32 bytes. (New Rule 21.)
- **Access-token lifetime.** Configured as `Jwt:ExpiryMinutes` (default **15**), not hours. The refresh-token rotation flow provides continuity. True access-token revocation (a `SecurityStamp`/token-version claim) is a pending DB-coordinated item (audit H8).
- **Workspace multi-tenancy.** Every workspace-scoped stored procedure takes `@WorkspaceId BIGINT = NULL` immediately after `@UserId` (or after a leading `@Id`); NULL resolves to the caller's personal workspace (back-compat). The active workspace is resolved per request from `UserWorkspacePreferences.CurrentWorkspaceId` via `IUserContext.WorkspaceId` and threaded through every scoped repository call. Reads/writes scope by `WorkspaceId`, not `UserId`. Authorization is enforced inside the SPs via `fn_CanAccessWorkspace`. Background analytics (FIL, Cash Flow) and report generation iterate **workspaces**, not users.
- **Rate limiting.** A global per-IP limiter plus a strict `"auth"` policy (applied to credential/token endpoints). `app.UseRateLimiter()` is in the pipeline.
- **Health checks.** `/health/live` (liveness) and `/health/ready` (DB connectivity via `ISqlConnectionFactory`), both anonymous and rate-limit-exempt.
- **API docs.** Swagger/OpenAPI (Swashbuckle) is mapped in Development only; gate it behind authorization before exposing it elsewhere.
- **Response hardening.** HSTS in non-Development plus baseline security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`).
- **Options validation.** `Jwt`, `Smtp`, and `Authentication` options use `AddOptions<T>().Bind().Validate(...).ValidateOnStart()` — misconfiguration aborts startup.
- **Async data access.** `ISqlConnectionFactory.CreateConnectionAsync` (`OpenAsync`) is the default path used by `DbExecutor`.
- **File storage.** Receipts and reports are **not** served by static-file middleware. They are reached only through authenticated endpoints or short-lived signed file links (`/api/files/...`). Only public assets (profile pictures, category icons) remain under `wwwroot/uploads` static serving.
- **Background jobs.** A batch is processed with bounded parallelism and a per-job timeout. Timer-based schedulers are gated behind `BackgroundJobs:RunSchedulers` (run on exactly one instance under horizontal scale); the queue processor stays on every instance.
- **Correlation + request logging.** `CorrelationIdMiddleware` runs first (echoes `X-Correlation-Id`, enriches logs). `RequestLoggingMiddleware` runs **after** authentication (so `UserId` is the authenticated caller) and only captures request bodies outside Production (no financial PII in the log store).
- **Domain layer.** Confirmed: Domain contains the exception hierarchy **only** — the previously-unused entity classes were removed.
- **Password hashing.** BCrypt enhanced, work factor **12**.

---

## Table of Contents

1. [Project Structure](#1-project-structure)
2. [ORM and Data Access](#2-orm-and-data-access)
3. [API Surface](#3-api-surface)
4. [Result and Response Contracts](#4-result-and-response-contracts)
5. [Validation Strategy](#5-validation-strategy)
6. [Localization Strategy](#6-localization-strategy)
7. [Authentication and Identity](#7-authentication-and-identity)
8. [Background Job Processing](#8-background-job-processing)
9. [Caching Strategy](#9-caching-strategy)
10. [Storage Strategy](#10-storage-strategy)
11. [Dependency Injection](#11-dependency-injection)
12. [Mapping Strategy](#12-mapping-strategy)
13. [Error Handling](#13-error-handling)
14. [Cross-Cutting Concerns](#14-cross-cutting-concerns)
15. [Rules That Must Never Be Violated](#15-rules-that-must-never-be-violated)

---

## 1. Project Structure

### Decision: Clean Architecture with five projects

The solution is split into `Shared`, `Domain`, `Application`, `Infrastructure`, and `WebApi`. Dependencies point strictly inward.

```
WebApi → Application ← Infrastructure
Application → Domain → Shared
```

**Reason:** Enforces a hard compile-time boundary between business logic and infrastructure concerns. Application defines *what* the system does through interfaces; Infrastructure provides *how* through concrete implementations. WebApi is a thin host that wires them together. This boundary makes it impossible for a service to accidentally import a `SqlConnection` or `HttpContext`.

**Accepted:**
- Every dependency flows inward. WebApi can reference Application and Infrastructure (composition root only). Application never references Infrastructure.
- Infrastructure references Application to implement its interfaces.

**Rejected:**
- Referencing Infrastructure from Application — defeats the inversion of control.
- Placing business logic in WebApi — endpoints are entry points only.
- Sharing a single project for Application and Infrastructure — loses the compile-time boundary.

### Decision: User model is split across Persons and Users tables

The `Persons` table holds personal/identity information. The `Users` table holds authentication and account-state information only. They are linked 1-to-1 via `PersonId`.

**Reason:** Separating identity from account allows future flexibility — SSO federation, multiple accounts per person, social login, or organizational membership structures — without a schema migration. Auth-related queries (login, token validation, lockout) never need to touch `Persons`. Profile queries never need to touch `PasswordHash` or `FailedLoginAttempts`.

**Accepted:**
- All auth operations (login, password reset, lockout) operate on `Users` only.
- All profile/personal data operations operate on `Persons` only.
- Domain/application code that needs full user info joins both tables through a repository method.

**Rejected:**
- Placing personal data fields (`FirstName`, `DateOfBirth`, `ProfilePicture`) on the `Users` table.
- Placing auth fields (`PasswordHash`, `IsLocked`) on the `Persons` table.

---

## 2. ORM and Data Access

### Decision: Dapper with stored procedures — no Entity Framework

All database access goes through `IDbExecutor`, which wraps Dapper and always uses `CommandType.StoredProcedure`. Every stored procedure is in the `MyMoney` schema and named `MyMoney.usp_{Feature}_{Action}`.

**Reason:** Stored procedures keep SQL centralized in the database where it can be tuned, audited, and version-controlled independently of the application code. Dapper's thin mapping layer avoids the query-generation surprises, migration complexity, and change-tracking overhead of EF Core. The `MyMoney` schema namespace prevents naming collisions and clearly scopes all application procedures.

**Accepted:**
- All queries through `IDbExecutor` — `ExecuteAsync`, `ExecuteScalarAsync`, `QuerySingleAsync`, `QueryListAsync`, `QueryMultipleAsync`.
- All stored procedures named `MyMoney.usp_{Feature}_{Action}`.
- `DynamicParameters` with explicit `DbType` on every parameter.
- Output parameters via `ParameterDirection.Output` + `p.Get<T>()`.

**Rejected:**
- Raw SQL strings (`CommandType.Text`) — no ad-hoc queries anywhere.
- Entity Framework Core — not installed, must not be added.
- Inline string-building for SQL — vulnerability and maintenance risk.
- Calling stored procedures through any path other than `IDbExecutor`.

---

## 3. API Surface

### Decision: Minimal APIs — no MVC controllers

Endpoints are static classes with extension methods on `IEndpointRouteBuilder`. Each feature group maps to one static class and one `Map{Feature}Endpoints` extension method registered in `Program.cs`.

**Reason:** Minimal APIs reduce MVC ceremony. Each endpoint is an explicit lambda — the full contract is visible in one place. Groups handle auth, tagging, and filtering at the scope level, reducing per-endpoint boilerplate.

**Accepted:**
- One static endpoint class per feature: `{Feature}Endpoints.cs`.
- One `MapGroup` per class, with tags and authorization set at group level where possible.
- All handler lambdas: `async (RequestType request, IService service, CancellationToken ct) => { ... }`.
- `result.ToHttpResponse()` as the only return expression in a handler.

**Rejected:**
- MVC `Controller` classes.
- `[HttpPost]`, `[FromBody]`, `[FromRoute]` attributes — Minimal API binds automatically.
- Returning `Results.Ok()`, `Results.BadRequest()`, or any `IResult` factory directly from a handler.
- Business logic inside endpoint lambdas — handlers call one service method and return.

### Decision: MediatR and CQRS were evaluated and rejected

**Reason:** The feature scope does not justify the indirection of a mediator dispatch pattern. Direct service injection is explicit and traceable. The same cross-cutting outcomes (validation, logging, error handling) are achieved through `ValidationFilter<T>`, Serilog middleware, and `GlobalExceptionHandler` at the WebApi layer.

**Rejected:**
- MediatR commands, queries, handlers, and pipeline behaviors.

---

## 4. Result and Response Contracts

### Decision: Two-layer result contract — `ServiceResult<T>` internally, `ApiResponse<T>` externally

Every service method returns `ServiceResult<T>`. Every endpoint converts it to `ApiResponse<T>` via `ServiceResultExtensions.ToHttpResponse()`. These two types are never mixed across their boundaries.

**Reason:** `ServiceResult<T>` carries the internal result code (`InternalResponseCodes`) and data without any HTTP concern. `ApiResponse<T>` is the stable JSON contract clients depend on. The mapping is isolated in `ServiceResultExtensions` — changing HTTP status code mapping never touches business logic.

```
ServiceResult<T>  →  ToHttpResponse()  →  ApiResponse<T>  →  HTTP Response
(Application)        (WebApi)             (Shared)
```

### Decision: `InternalResponseCodes` is a custom byte enum, not HTTP status codes

Services use `InternalResponseCodes.OK`, `.Created`, `.Conflict`, `.NotFound`, etc. These are mapped to HTTP status codes only in `ServiceResultExtensions.MapHttpStatus()`.

**Reason:** Services must not know about HTTP. Using `InternalResponseCodes` keeps the Application layer transport-agnostic. A service returning `InternalResponseCodes.Conflict` expresses a domain fact; the HTTP `409` is a presentation concern.

**Accepted:**
- `ServiceResultFactory.Success(data, code, message)` — success with payload.
- `ServiceResultFactory.Success<object?>(code, message)` — success with no payload (void operations).
- `ServiceResultFactory.Failure<T>(code, message)` — any business failure.

**Rejected:**
- Returning `null` from a service method to indicate failure.
- Throwing exceptions from services to signal business failures.
- Constructing `ApiResponse<T>` inside service or repository code.
- Using `Results.*` factory methods directly in endpoint handlers.
- Returning HTTP status codes directly from services.

---

## 5. Validation Strategy

### Decision: FluentValidation at the endpoint filter layer, not in services

Each request type that requires validation has a `{Request}Validator` class. Validators are applied to endpoints via `AddEndpointFilter<ValidationFilter<TRequest>>()`. The filter runs before the handler and short-circuits with a failure response on invalid input.

**Reason:** Validation of input shape (required fields, formats, ranges) is a presentation concern. Keeping validators out of services means services receive structurally valid input by contract.

**Accepted:**
- `public sealed class {Request}Validator : AbstractValidator<{Request}>` in `Application/Features/{Feature}/Validations/`.
- All `WithMessage()` values are `MessageKeys.*` string constants — never inline strings.
- Validators registered by assembly scan in `Program.cs`.
- Business-level checks (duplicate email, balance checks) performed in the service, not in validators.

**Rejected:**
- Inline validation inside endpoint lambdas.
- Calling `validator.Validate()` manually inside services.
- Inline strings in `WithMessage()` — they will not be localized.
- Adding `ValidationFilter<T>` to endpoints that take no request body.

---

## 6. Localization Strategy

### Decision: All user-facing strings live in JSON files, keyed by constants, resolved at runtime

`system-messages.json` holds flat `key → string` pairs per language. `system-labels.json` holds `section → key → string` per language. `IMessageProvider` reads, deserializes, and caches both files. `MessageKeys` in `Shared` holds the string constants for every key.

**Reason:** The system supports English and Arabic. JSON files allow content updates without code changes. In-process caching ensures file reads happen at most once per instance lifetime. Centralizing keys in `MessageKeys` gives compile-time safety.

**Accepted:**
- All message keys declared as `const string` in `Shared/Constants/MessageKeys.cs` under a nested static class per feature.
- Messages retrieved as: `await messageProvider.GetMessagesAsync(MessageKeys.Feature.Key, ct)`.
- UI labels retrieved as: `await messageProvider.GetLabelsAsync("Feature.Section", ct)`.
- Language resolved from the `Accept-Language` HTTP header via `IUserContext.Language` (`Arabic` or `English`).

**Rejected:**
- Hardcoded strings anywhere in service or endpoint code.
- Inline strings in `WithMessage()` in validators.
- Reading JSON localization files outside of `MessageProvider`.
- Embedding language logic anywhere other than `UserContext` and `MessageProvider`.

---

## 7. Authentication and Identity

### Decision: JWT Access Tokens + Opaque Refresh Token rotation

**Access tokens** are short-lived JWTs (configurable, default 15 minutes) issued by `IJwtService`. They carry `NameId` (UserId), `Email`, `DisplayName`, `role`, and `jti` claims. `ClockSkew = TimeSpan.Zero`.

**Refresh tokens** are long-lived (configurable, default 7 days) opaque random strings stored in `MyMoney.RefreshTokens`. The table stores a **SHA-256 hash** of the raw token — the raw token is only ever sent to the client and never persisted. On refresh, the old token is revoked and a new pair is issued (rotation). Multiple active tokens per user are supported to allow multiple devices.

**Reason:** Short-lived access tokens limit the blast radius of token theft. Refresh token rotation means a stolen refresh token is invalidated the moment the legitimate client tries to rotate, triggering a forced re-authentication. Storing only the hash (not the raw token) prevents database breaches from yielding usable tokens.

**Accepted:**
- Caller identity read exclusively through `IUserContext` — never `HttpContext.User` directly in Application code.
- `IUserContext` resolves `UserId`, `Email`, `DisplayName`, `RoleId`, `Language`, `IpAddress`, `UserAgent`, `SessionId`, `TraceId`.
- Access token generation via `IJwtService.GenerateAccessToken(JwtTokenResponse)` only.
- Refresh token generation, validation, and rotation via `IAuthTokenService` (wraps the refresh token repository).
- Token hashing via `ITokenHasher` (SHA-256) — not BCrypt — tokens are random and long, BCrypt is for passwords.

**Rejected:**
- Accessing `HttpContext` from Application or Domain layers.
- Session-based authentication.
- Storing raw refresh tokens in the database.
- Storing refresh tokens in the JWT itself.
- Issuing long-lived access tokens instead of using the refresh token mechanism.

### Decision: Account lockout after repeated failed login attempts

After a configurable number of failed attempts (default: 5), the account is locked until `LockoutEndDateUtc` (default: 15 minutes from last failure). The lockout state and counter live entirely in the `Users` table and are managed atomically by the stored procedure.

**Reason:** Prevents brute-force attacks on passwords. Atomic SP handling avoids race conditions where concurrent login attempts could bypass the counter.

**Accepted:**
- Lockout check and counter increment handled in a single stored procedure call.
- `IUserContext.IsAuthenticated` must be checked in services that require an authenticated user.
- On successful login: reset `FailedLoginAttempts` to 0, update `LastLoginDateUtc`.

**Rejected:**
- Checking lockout state in the application layer before calling the database — the SP is the single source of truth.

### Decision: Language from `Accept-Language` header, not from JWT claims

Language is read on every request from the `Accept-Language` header (`"en"` prefix = English, anything else = Arabic). It is not embedded in the JWT.

**Accepted:** `IUserContext.Language` is the single access point for language resolution.

**Rejected:** Embedding language in JWT claims. Reading `Accept-Language` anywhere except `UserContext`.

---

## 8. Background Job Processing

### Decision: Database-backed outbox table with a hosted service processor

All asynchronous work (emails, notifications, reports) is enqueued as a row in `MyMoney.BackgroundJobs` via `IBackgroundJobService.EnqueueAsync(jobType, payload, scheduledAt?, priority?)`. A hosted service (`BackgroundJobProcessor`) polls the table on a configurable interval, picks up due jobs, dispatches them to registered `IJobHandler<TPayload>` implementations, and marks them completed or failed.

**Reason:** A database-backed queue requires no additional infrastructure dependencies (no Redis, no Service Bus, no RabbitMQ). The table serves as both a queue and an audit log — every job that was ever enqueued, when it ran, how many attempts it took, and any error messages are permanently visible. The `IBackgroundJobService` and `IJobHandler<TPayload>` abstractions mean the transport can be swapped to a real message broker later without changing any feature code.

**Job lifecycle:**

```
Enqueue  →  Pending (1)
            ↓ (processor picks up)
         Processing (2)
            ↓ success            ↓ failure (attempt < max)    ↓ failure (attempt >= max)
         Completed (3)        Failed (4) + NextRetryAt set    Failed (4) permanent
```

**Accepted:**
- All async work goes through `IBackgroundJobService.EnqueueAsync` — never a direct method call or `Task.Run`.
- Payload is serialized to JSON (using `System.Text.Json`) before storage.
- Each job type has exactly one `IJobHandler<TPayload>` registered in DI.
- `BackgroundJobProcessor` is registered as a hosted service (`IHostedService`) in `InfrastructureRegistration`.
- Job handlers are `internal sealed` and registered in `InfrastructureRegistration`.
- Failed jobs with `AttemptCount < MaxAttempts` have `NextRetryAt` set with exponential back-off.
- `MaxAttempts` defaults to 3 but can be overridden per job type at enqueue time.
- `Priority` (1=High, 2=Normal, 3=Low) influences processor pick-up order.

**Rejected:**
- `Task.Run` or `fire-and-forget` patterns in services — these lose durability on app restart.
- Calling email/notification providers directly from service code without enqueuing.
- Sharing a single large job payload shape — each job type has its own strongly-typed payload record.
- Polling intervals under 5 seconds — respects database I/O budget.

### Decision: Password reset uses the background job infrastructure

When a user requests a password reset, the service:
1. Generates a cryptographically random token (GUID + timestamp hash).
2. Stores the **hashed** token in `MyMoney.BackgroundJobs` payload alongside the user's email and expiry.
3. Enqueues a `PasswordResetEmail` job with `Priority = High`.
4. Returns `OK` to the caller immediately (never reveals whether the email exists).

The job handler reads the token from the payload, constructs the reset link, and sends the email.

**Reason:** Decouples the email-sending concern from the request lifecycle. The user gets an instant response regardless of email provider latency. If the email provider is down, the job retries automatically. This pattern is reusable for welcome emails, monthly reports, and any future notification type.

**Rejected:**
- A dedicated `PasswordResetTokens` table — the token lives in the job payload and is only valid for the duration of the job's `ScheduledAt + ExpiryMinutes` window.
- Sending emails synchronously from services — creates latency and failure coupling.
- Calling the email provider from within the HTTP request pipeline.

---

## 9. Caching Strategy

### Decision: In-process `IMemoryCache` with 12-hour default TTL

`MemoryCacheService` wraps `IMemoryCache`. Default expiration is 12 hours unless overridden at the call site. Registered as Singleton.

**Reason:** The primary cached content — category lists, localization files — is reference data that rarely changes and is safe to serve stale within a deployment cycle. In-process cache avoids the network latency and serialization overhead of a distributed cache. Cache invalidation of reference data is handled by redeployment or explicit `RemoveAsync` calls.

**Accepted:**
- Cache keys are lowercase, colon-separated strings with variant suffixes (e.g., language, filter key).
- Cached lists cast to `IReadOnlyList<T>` before storing and retrieved as `IReadOnlyList<T>`.
- Pattern: check cache → on miss, fetch from DB → store in cache → return.
- Explicit cache invalidation via `ICacheService.RemoveAsync(key)` after mutations that affect cached data.

**Rejected:**
- Redis or any distributed cache — not warranted at current scale.
- Per-user caching of volatile transaction data — cache is for reference/lookup data only.
- Type mismatch between `SetAsync<T>` and `GetAsync<T>` calls.

---

## 10. Storage Strategy

### Decision: Local filesystem storage inside `wwwroot/uploads/` — no external cloud storage

User-uploaded files (profile pictures) and system assets (category icons) are stored on the local filesystem under `WebApi/wwwroot/uploads/`. Files are served directly by ASP.NET Core's static file middleware. `IFileService` is the only way to read or write files from Application code.

**Reason:** The application is a personal finance tool with modest storage needs. Local storage eliminates dependency on external services, simplifies local development, and has zero per-request cost. The `IFileService` abstraction means cloud storage (Azure Blob, S3) can be swapped in later by adding a new Infrastructure implementation without touching any feature code.

**Folder structure (configured in `appsettings.json` under `Storage:FolderPaths`):**

```
wwwroot/
  uploads/
    profiles/    — user profile pictures
    icons/       — category icons
```

**URL construction:** `IStorageUtility.BuildFilePathWithExpiration(folderPath, fileName, isInternalStorage: true, baseUrl: ...)` returns the public URL. This is the only place URLs are assembled.

**Accepted:**
- All file reads/writes go through `IFileService` — never `System.IO.File` directly in Application code.
- File names stored in the database are relative names only (e.g., `avatar_123.jpg`), never absolute paths or full URLs.
- Public URLs are constructed at response time from the stored file name.
- `IStorageUtility` is used to resolve folder paths from configuration.

**Rejected:**
- Storing absolute paths or full URLs in the database.
- Reading/writing files from the filesystem directly in Application services.
- Exposing `wwwroot` physical paths to clients.
- Cloud storage providers — not installed, must not be added unless `IFileService` is re-implemented.

---

## 11. Dependency Injection

### Decision: Explicit manual registration in layer extension methods

Application services are registered in `ServiceCollectionExtensions.AddApplication()`. Infrastructure services and repositories are registered in `InfrastructureRegistration.AddInfrastructure()`. `Program.cs` calls only these two methods.

**Accepted lifetimes:**

| Type | Lifetime | Reason |
|---|---|---|
| `ISqlConnectionFactory` | Singleton | Stateless; creates connections on demand |
| `IJwtService` | Singleton | Stateless; key material loaded once |
| `IPasswordHasher` | Singleton | Stateless |
| `ITokenHasher` | Singleton | Stateless; wraps SHA-256 |
| `ICacheService` | Singleton | Wraps `IMemoryCache`, which is Singleton |
| `IStorageUtility` | Singleton | Stateless; reads config once |
| `IFileService` | Singleton | Stateless; no per-request state |
| `IDbExecutor` | Scoped | Opens and closes a `SqlConnection` per request |
| All repositories | Scoped | Depend on Scoped `IDbExecutor` |
| All application services | Scoped | May depend on Scoped `IUserContext` |
| `IUserContext` | Scoped | Reads `HttpContext` which is per-request |
| `IMessageProvider` | Scoped | Reads `IUserContext` for language resolution |
| `IBackgroundJobService` | Scoped | Writes to DB via scoped `IDbExecutor` |
| `BackgroundJobProcessor` | Singleton (IHostedService) | Long-running hosted service; creates its own DI scopes per job |

**Rejected:**
- Singleton service taking a Scoped dependency (captive dependency anti-pattern).
- Assembly scanning for service or repository registration — explicit registration is mandatory.
- Registering Infrastructure types in `Program.cs` directly.

---

## 12. Mapping Strategy

### Decision: Manual LINQ projections — no AutoMapper or Mapster

All DB model → Response mapping is written as explicit LINQ `Select` projections in the service. There is no mapping library in the solution.

**Reason:** Mapping in this codebase involves URL construction, localization label injection, and dictionary joins across multiple result sets. Reflection-based mappers cannot express these cleanly. Explicit projections make every mapping decision visible and reviewable.

**Accepted:**
- `dbResult.Select(r => new MyResponse(...)).ToList()` directly in the service.
- Dictionary and `ToLookup` joins within the service when combining multiple result sets.
- `IStorageUtility`/`IFileService` calls embedded in projection lambdas for asset URLs.
- Label dictionary lookups with `TryGetValue` + string fallback.
- Cast to `IReadOnlyList<T>` before caching projected lists.

**Rejected:**
- AutoMapper, Mapster, or any reflection-based object mapper.
- Mapping in repositories — repositories return raw DB types.
- Mapping in endpoint lambdas — endpoints call one service method and return.

---

## 13. Error Handling

### Decision: Domain exceptions propagate upward; business failures return `ServiceResultFactory.Failure`

The exception hierarchy (`DomainException`, `NotFoundException`, `ForbiddenException`, `ValidationAppException`) exists for programmer-level invariant violations. Business outcomes like "email already in use" or "insufficient funds" are returned as `ServiceResult` failures.

**Reason:** Exceptions for flow control are expensive and obscure the happy path. `ServiceResult.Failure` is explicit, self-documenting, and forces callers to handle the failure case. `GlobalExceptionHandler` is the safety net for genuinely unexpected states.

**`GlobalExceptionHandler` mapping:**

| Exception | HTTP Status | InternalCode |
|---|---|---|
| `ValidationAppException` | 200 | `BadRequest` |
| `UnauthorizedAccessException` | 200 | `Unauthorized` |
| `ForbiddenException` | 200 | `Forbidden` |
| `NotFoundException` | 200 | `NotFound` |
| `DomainException` | 200 | `BadRequest` |
| Anything else | 500 | `InternalServerError` |

**Accepted:**
- `ServiceResultFactory.Failure<T>(InternalResponseCodes.X, message)` for all business failures.
- Domain exceptions thrown only for genuine programmer-level contract violations.
- `GlobalExceptionHandler` as the last-resort safety net — not as primary error handling.

**Rejected:**
- Throwing `NotFoundException` or `ForbiddenException` for normal service flow (e.g., "user not found at login" is a business failure, not a domain exception).
- `try/catch` blocks inside services for expected business conditions.
- Swallowing exceptions silently.
- Returning `null` from services.

---

## 14. Cross-Cutting Concerns

### Correlation ID

Every request is assigned a correlation ID from the `X-Correlation-Id` header (or a generated GUID if absent). It is propagated to the response header and pushed to Serilog's `LogContext`. All log entries for a request share the same correlation ID.

### Request Logging

`RequestLoggingMiddleware` logs path, method, body, `UserId`, and IP address via Serilog on every request. Body is redacted for endpoints whose path contains `/login` or `/reset`. Multipart bodies and bodies over 32 KB are not logged.

### Structured Logging

Serilog is configured from `appsettings.json` with a Console sink and an MSSqlServer sink. The Serilog host integration (`UseSerilog()`) replaces the default ASP.NET Core logging infrastructure.

**Accepted:** All logging through Serilog's `ILogger<T>` or `Log.Logger`.

**Rejected:** `Console.WriteLine`, `Debug.WriteLine`, or direct `ILogger` calls that bypass Serilog enrichment.

---

## 15. Rules That Must Never Be Violated

| # | Rule |
|---|---|
| **1** | **Application never references Infrastructure.** Any code that imports an Infrastructure namespace from Application is a violation. |
| **2** | **No raw SQL.** All database access uses named stored procedures through `IDbExecutor`. `CommandType.Text` is forbidden. |
| **3** | **Services never throw for business failures.** Return `ServiceResultFactory.Failure<T>` for every expected failure condition. |
| **4** | **Endpoints never return `Results.*` directly.** All handlers end with `result.ToHttpResponse()`. |
| **5** | **All validator `WithMessage()` values are `MessageKeys.*` constants.** Inline strings bypass localization. |
| **6** | **All file URLs are built through `IStorageUtility`.** No inline path construction in services. |
| **7** | **`IUserContext` is the only way to access caller identity from Application.** `HttpContext` must not be imported into any Application class. |
| **8** | **Service and repository implementations are `internal sealed`.** Contracts are exposed through interfaces only. |
| **9** | **`DynamicParameters` always declares `DbType` explicitly.** Omitting it invites silent type coercion bugs in SQL Server. |
| **10** | **Cache `SetAsync` and `GetAsync` must use the same concrete type.** Cast projected lists to `IReadOnlyList<T>` before storing. |
| **11** | **New message keys must be declared in `MessageKeys` before use.** Never pass literal strings to `IMessageProvider`. |
| **12** | **`InternalResponseCodes` maps to HTTP status codes in one place only** — `ServiceResultExtensions.MapHttpStatus()`. |
| **13** | **Repositories return DB model types only.** Mapping to response types happens in the service, never in the repository. |
| **14** | **New application services go in `AddApplication()`; new repositories go in `AddInfrastructure()`.** Never register them in `Program.cs`. |
| **15** | **Entity Framework is not used and must not be added.** The database layer is owned by Dapper + stored procedures. |
| **16** | **All async work goes through `IBackgroundJobService.EnqueueAsync`.** No `Task.Run`, no fire-and-forget from services. |
| **17** | **Raw refresh tokens are never stored in the database.** Only the SHA-256 hash is persisted in `RefreshTokens.Token`. |
| **18** | **File system access uses `IFileService` exclusively.** `System.IO.File` must not be used directly in Application services. |
| **19** | **Passwords are hashed with BCrypt via `IPasswordHasher`.** Tokens are hashed with SHA-256 via `ITokenHasher`. Never use MD5 or SHA-1. |
| **20** | **Stored procedure names follow `MyMoney.usp_{Feature}_{Action}` exactly.** No deviations in casing or schema prefix. |
| **21** | **Secrets never live in `appsettings.json`.** JWT key, SMTP credentials, and production connection strings come from user-secrets (dev) or environment variables (prod). Startup fails fast if the JWT key is absent or < 32 bytes. |
| **22** | **Workspace-scoped SPs take `@WorkspaceId BIGINT = NULL` after `@UserId`.** Resolve the active workspace from `IUserContext.WorkspaceId` and pass it on every scoped call; scope rows by `WorkspaceId`, never `UserId`. |
| **23** | **Sensitive uploaded files (receipts, reports) are never served by static-file middleware.** Reach them only via authenticated endpoints or signed file links. |
