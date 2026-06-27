# MyMoney API — Development Guide

This document is the authoritative source of truth for all development on this codebase. Every new feature, endpoint, service, and repository **must** follow the patterns established here. When in doubt, refer to the nearest existing feature for guidance.

> **Remediation addendum (2026-06-28).** The enterprise audit added cross-cutting patterns that every new feature must respect. See `ARCHITECTURE_DECISIONS.md` §0 for the full list. The ones that change day-to-day implementation:
> - **Workspace scoping.** Workspace-scoped repository calls pass `@WorkspaceId` (from `IUserContext.WorkspaceId`) right after `@UserId`; the SP treats `NULL` as the caller's personal workspace. Scope by workspace, not user. Background analytics/report jobs iterate workspaces.
> - **Secrets** come from user-secrets/env, never `appsettings.json` (the JWT key is validated at startup).
> - **Data access** uses `IDbExecutor` over `ISqlConnectionFactory.CreateConnectionAsync` (async open).
> - **Pipeline** (in order): `CorrelationIdMiddleware` → security headers → rate limiter → auth → `RequestLoggingMiddleware` (after auth; bodies logged in Development only). Health (`/health/live`, `/health/ready`) and Swagger (Development) are mapped.
> - **Sensitive files** (receipts, reports) are served only through authenticated endpoints or signed links — never static-file middleware.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Layer Responsibilities](#2-layer-responsibilities)
3. [Request Lifecycle](#3-request-lifecycle)
4. [Feature Implementation Pattern](#4-feature-implementation-pattern)
5. [Service Implementation Pattern](#5-service-implementation-pattern)
6. [Repository Pattern](#6-repository-pattern)
7. [Validation Pattern](#7-validation-pattern)
8. [Mapping Pattern](#8-mapping-pattern)
9. [Background Job Pattern](#9-background-job-pattern)
10. [Naming Conventions](#10-naming-conventions)
11. [Coding Conventions](#11-coding-conventions)
12. [Common Pitfalls](#12-common-pitfalls)
13. [Codebase Examples](#13-codebase-examples)

---

## 1. Architecture Overview

The solution follows **Clean Architecture** across five projects. Dependencies flow strictly inward.

```
┌──────────────────────────────────────────────────────┐
│  WebApi                                              │
│  Endpoints · Filters · Middlewares · ExceptionHandler│
└──────────────────┬───────────────────────────────────┘
                   │ depends on
┌──────────────────▼───────────────────────────────────┐
│  Infrastructure                                      │
│  DbExecutor · Repositories · JwtService · Cache      │
│  MessageProvider · FileService · StorageUtility      │
│  BackgroundJobProcessor · JobHandlers                │
└──────────────────┬───────────────────────────────────┘
                   │ implements interfaces from
┌──────────────────▼───────────────────────────────────┐
│  Application                                         │
│  Services · DTOs · Validators · Repository Interfaces│
│  Service Interfaces · DB Models                      │
│  IBackgroundJobService · IJobHandler<TPayload>       │
└──────────────────┬───────────────────────────────────┘
                   │ depends on
┌──────────────────▼───────────────────────────────────┐
│  Domain                                              │
│  Exceptions (DomainException, NotFoundException …)   │
└──────────────────┬───────────────────────────────────┘
                   │ depends on
┌──────────────────▼───────────────────────────────────┐
│  Shared                                              │
│  Enums · Constants · ApiResponse · ServiceResult     │
│  MessageKeys · InternalResponseCodes                 │
└──────────────────────────────────────────────────────┘
```

**Database:** SQL Server via **Dapper only**. All queries go through **named stored procedures** in the `MyMoney` schema. Entity Framework is not used.

**API style:** Minimal APIs (no controllers). Every feature group is a static class with an extension method on `IEndpointRouteBuilder`.

---

## 2. Layer Responsibilities

### Shared
Contains everything that all layers need but carries no business logic.

- `ApiResponse<T>` — JSON envelope returned by every endpoint
- `ServiceResult<T>` — internal result type returned by every service method
- `ServiceResultFactory` — static factory for `ServiceResult<T>` instances
- `InternalResponseCodes` — custom codes that map to HTTP status codes
- `MessageKeys` — string constants for all localization keys
- Enums: `SystemRoles`, `SystemLanguages`, `TransactionTypes`, `GenderTypes`, `BackgroundJobStatus`, `BackgroundJobPriority`

### Domain
Contains the exception hierarchy only. No entity classes are needed since Dapper maps directly to DB model classes in Application.

- `DomainException`, `NotFoundException`, `ForbiddenException`, `ValidationAppException`

Domain exceptions are caught by `GlobalExceptionHandler` and translated to `ApiResponse<object?>` failure responses automatically.

### Application
Contains all business logic. This layer defines **what** the system does. It never references Infrastructure directly.

- **Feature services** (`I{Feature}Service` interface + `{Feature}Service` implementation)
- **DTOs** — `{Action}Request` and `{Action}Response` record types
- **Validators** — `{Action}Validator` using FluentValidation
- **Repository interfaces** — `I{Feature}Repository`
- **DB Models** — `{Action}DbModel` (input) and `{Action}DbResult` (output)
- **Service interfaces** — `IUserContext`, `ICacheService`, `IJwtService`, `IFileService`, `IMessageProvider`, `IBackgroundJobService`, etc.
- **Job payload records** — strongly-typed payload types for each background job

### Infrastructure
Contains all **how** — concrete implementations of Application interfaces.

- `DbExecutor` — Dapper wrapper; all DB methods use `CommandType.StoredProcedure`
- `SqlConnectionFactory` — creates `SqlConnection` from configuration
- Repository implementations
- `JwtService`, `PasswordHasher`, `TokenHasher`, `UserContext`
- `MemoryCacheService` — wraps `IMemoryCache` (12-hour default TTL)
- `MessageProvider` — reads and caches localization JSON files
- `LocalFileService` — implements `IFileService` using the local filesystem
- `StorageUtility` — resolves folder paths from configuration
- `BackgroundJobProcessor` — hosted service that polls and executes background jobs
- `IJobHandler<TPayload>` implementations — one per job type

### WebApi
The composition root and HTTP surface.

- `Program.cs` — registers services, configures middleware, maps all endpoints
- Endpoint classes — static `{Feature}Endpoints` classes
- `ValidationFilter<TRequest>` — runs FluentValidation before the handler
- `GlobalExceptionHandler` — catches domain exceptions and unhandled errors
- `CorrelationIdMiddleware` — assigns/propagates a correlation ID to every request
- `RequestLoggingMiddleware` — logs path, method, body, and user context via Serilog
- `ServiceResultExtensions.ToHttpResponse()` — converts `ServiceResult<T>` to `IResult`

---

## 3. Request Lifecycle

```
HTTP Request
  │
  ▼
CorrelationIdMiddleware
  Reads X-Correlation-Id header (or generates a new GUID)
  Appends it to the response header
  Pushes it to Serilog LogContext
  │
  ▼
RequestLoggingMiddleware
  Logs: path, method, body (redacted for /login and /reset), UserId, IP
  │
  ▼
Authentication / Authorization middleware
  Validates JWT Bearer token
  Populates HttpContext.User with claims
  │
  ▼
Minimal API Endpoint lambda
  Receives deserialized request
  │
  ▼
ValidationFilter<TRequest>  (if endpoint has a validator)
  Resolves IValidator<TRequest> from DI
  Runs ValidateAsync()
  On failure: translates each error.ErrorMessage (a MessageKey) via IMessageProvider
  Returns ApiResponse<object?>.Fail(code, "msg1 | msg2") immediately
  │
  ▼
I{Feature}Service.{Action}Async(request, ct)
  Reads caller identity from IUserContext
  Checks ICacheService for cached data
  Maps request → DbModel
  Calls I{Feature}Repository
  Maps DbResult → Response record(s) via LINQ projection
  Resolves localized messages via IMessageProvider
  Returns ServiceResultFactory.Success / Failure
  │
  ▼
result.ToHttpResponse()
  Maps InternalResponseCodes → HTTP status code
  Wraps in ApiResponse<T>
  Returns Results.Json(response, statusCode: httpStatus)
  │
  ▼
HTTP Response (JSON)
```

---

## 4. Feature Implementation Pattern

Every new feature requires changes across all five projects. Follow this checklist in order.

### Step 1 — Shared: Enums (if needed)

Add new enum files under `Shared/Enums/{Domain}/`.

### Step 2 — Shared: Message Keys

Add a new nested static class to `Shared/Constants/MessageKeys.cs`:

```csharp
public static class MyFeature
{
    public const string NotFound            = "MyFeature.NotFound";
    public const string CreatedSuccessfully = "MyFeature.CreatedSuccessfully";
    // one constant per distinct user-facing message
}
```

### Step 3 — Application: DB Models

Create `Application/Interfaces/Repositories/DbModels/{Feature}/{Models}.cs`:

```csharp
public class CreateMyItemDbModel
{
    public long   UserId { get; set; }
    public string Name   { get; set; } = null!;
}

public class CreateMyItemDbResult
{
    public long Id        { get; set; }
    public bool IsSuccess { get; set; }
}
```

### Step 4 — Application: Repository Interface

Create `Application/Interfaces/Repositories/I{Feature}Repository.cs`:

```csharp
public interface IMyFeatureRepository
{
    Task<CreateMyItemDbResult?> CreateAsync(CreateMyItemDbModel model, CancellationToken ct);
    Task<IReadOnlyList<MyItemDbResult>> GetListAsync(long userId, CancellationToken ct);
}
```

### Step 5 — Application: Service Interface

Create `Application/Features/{Feature}/I{Feature}Service.cs`:

```csharp
public interface IMyFeatureService
{
    Task<ServiceResult<MyItemResponse>>              CreateAsync(CreateMyItemRequest request, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<MyItemResponse>>> GetListAsync(CancellationToken ct);
}
```

### Step 6 — Application: DTOs

Create `Application/Features/{Feature}/DTOs/{Action}Dto.cs` (request and response for the same action in one file):

```csharp
public record CreateMyItemRequest(string Name, int CategoryId);

public record MyItemResponse(
    long   Id,
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? OptionalField
);
```

### Step 7 — Application: Validator

Create `Application/Features/{Feature}/Validations/{Action}Validator.cs`:

```csharp
public sealed class CreateMyItemValidator : AbstractValidator<CreateMyItemRequest>
{
    public CreateMyItemValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(MessageKeys.MyFeature.NameRequired);
    }
}
```

### Step 8 — Application: Service

Create `Application/Features/{Feature}/{Feature}Service.cs`. Register in `Application/Common/Extensions/ServiceCollectionExtensions.cs`.

### Step 9 — Infrastructure: Repository

Create `Infrastructure/Persistence/{Feature}/{Feature}Repository.cs`. Register in `Infrastructure/InfrastructureRegistration.cs`.

### Step 10 — WebApi: Endpoint

Create `WebApi/Endpoints/{Feature}Endpoints.cs`. Register in `WebApi/Program.cs`.

### Step 11 — Database

Create stored procedure `MyMoney.usp_{Feature}_{Action}`.

### Step 12 — Localization

Add all new message keys to `wwwroot/resources/system-messages.json` under both `ar` and `en`.

---

## 5. Service Implementation Pattern

### Class declaration

```csharp
// Always: internal sealed, primary constructor
internal sealed class MyFeatureService(
    IMyFeatureRepository myRepository,
    IUserContext         userContext,
    ICacheService        cacheService,
    IMessageProvider     messageProvider) : IMyFeatureService
{
    private const string ListCacheKey = "myfeature:list";
}
```

### Read with caching

```csharp
public async Task<ServiceResult<IReadOnlyList<MyItemResponse>>> GetListAsync(CancellationToken ct)
{
    var language = userContext.Language;
    var cacheKey = $"{ListCacheKey}:{(int)language}";

    var cached = await cacheService.GetAsync<IReadOnlyList<MyItemResponse>>(cacheKey);
    if (cached is not null)
    {
        return ServiceResultFactory.Success(
            cached,
            InternalResponseCodes.OK,
            await messageProvider.GetMessagesAsync(MessageKeys.Common.Success, ct));
    }

    var dbResult = await myRepository.GetListAsync(userContext.UserId, ct);

    var response = dbResult
        .Select(r => new MyItemResponse(r.Id, r.Name))
        .ToList();

    await cacheService.SetAsync(cacheKey, (IReadOnlyList<MyItemResponse>)response);

    return ServiceResultFactory.Success<IReadOnlyList<MyItemResponse>>(
        response,
        InternalResponseCodes.OK,
        await messageProvider.GetMessagesAsync(MessageKeys.Common.Success, ct));
}
```

### Write with failure handling

```csharp
public async Task<ServiceResult<MyItemResponse>> CreateAsync(
    CreateMyItemRequest request,
    CancellationToken   ct)
{
    var isDuplicate = await myRepository.ExistsAsync(userContext.UserId, request.Name, ct);
    if (isDuplicate)
    {
        return ServiceResultFactory.Failure<MyItemResponse>(
            InternalResponseCodes.Conflict,
            await messageProvider.GetMessagesAsync(MessageKeys.MyFeature.AlreadyExists, ct));
    }

    var dbModel = new CreateMyItemDbModel
    {
        UserId = userContext.UserId,
        Name   = request.Name
    };

    var result = await myRepository.CreateAsync(dbModel, ct);

    // NULL: parent record not found, FK violation, or SP returned no rows
    if (result is null)
    {
        return ServiceResultFactory.Failure<MyItemResponse>(
            InternalResponseCodes.NotFound,
            await messageProvider.GetMessagesAsync(MessageKeys.MyFeature.NotFound, ct));
    }

    // Zero-Id: transaction rolled back inside the SP
    if (result.Id == 0)
    {
        return ServiceResultFactory.Failure<MyItemResponse>(
            InternalResponseCodes.InternalServerError,
            await messageProvider.GetMessagesAsync(MessageKeys.Common.InternalServerError, ct));
    }

    return ServiceResultFactory.Success(
        new MyItemResponse(result.Id, request.Name),
        InternalResponseCodes.Created,
        await messageProvider.GetMessagesAsync(MessageKeys.MyFeature.CreatedSuccessfully, ct));
}
```

### Void operation

```csharp
public async Task<ServiceResult<object?>> DeleteAsync(int id, CancellationToken ct)
{
    var dbModel = new DeleteMyItemDbModel
    {
        UserId = userContext.UserId,
        ItemId = id
    };

    await myRepository.DeleteAsync(dbModel, ct);

    return ServiceResultFactory.Success<object?>(
        null,
        InternalResponseCodes.OK,
        await messageProvider.GetMessagesAsync(MessageKeys.Common.Deleted, ct));
}
```

### Key service rules

- **Never throw** for business failures. Use `ServiceResultFactory.Failure<T>`.
- **Always pass `CancellationToken ct`** as the last parameter of every public method.
- **Never return raw DB model types** from a service. Always map to a Response record.
- **Never construct HTTP-related types** (`IResult`, `Results.*`) in a service.
- **Get caller identity from `IUserContext`**, never from `HttpContext` directly.
- Use `ServiceResultFactory.Success<object?>(null, code, message)` for void operations.

---

## 6. Repository Pattern

### Class declaration

```csharp
// Always: internal sealed, primary constructor, single IDbExecutor dependency
internal sealed class MyFeatureRepository(IDbExecutor db) : IMyFeatureRepository
{
}
```

### Single-row query

```csharp
public async Task<MyItemDbResult?> GetByIdAsync(long id, CancellationToken ct)
{
    var p = new DynamicParameters();
    p.Add("@Id", id, DbType.Int64);

    return await db.QuerySingleAsync<MyItemDbResult>(
        "MyMoney.usp_MyFeature_GetById", p, ct);
}
```

### List query

```csharp
public async Task<IReadOnlyList<MyItemDbResult>> GetListAsync(long userId, CancellationToken ct)
{
    var p = new DynamicParameters();
    p.Add("@UserId", userId, DbType.Int64);

    return await db.QueryListAsync<MyItemDbResult>(
        "MyMoney.usp_MyFeature_GetList", p, ct);
}
```

### Paginated / filtered list query

```csharp
public async Task<GetTransactionsDbResult> GetListAsync(GetTransactionsDbModel model, CancellationToken ct)
{
    var p = new DynamicParameters();
    p.Add("@UserId",      model.UserId,               DbType.Int64);
    p.Add("@TypeId",      model.TypeId.HasValue
                              ? (byte?)model.TypeId   : null, DbType.Byte);
    p.Add("@CategoryId",  model.CategoryId,            DbType.Int32);
    p.Add("@DateFrom",    model.DateFrom,              DbType.Date);
    p.Add("@DateTo",      model.DateTo,                DbType.Date);
    p.Add("@AmountMin",   model.AmountMin,             DbType.Decimal);
    p.Add("@AmountMax",   model.AmountMax,             DbType.Decimal);
    p.Add("@SortBy",      model.SortBy,                DbType.String);
    p.Add("@SortDir",     model.SortDir,               DbType.String);
    p.Add("@PageNumber",  model.PageNumber,            DbType.Int32);
    p.Add("@PageSize",    model.PageSize,              DbType.Int32);
    p.Add("@TotalCount",  dbType: DbType.Int32, direction: ParameterDirection.Output);

    var items = await db.QueryListAsync<TransactionDbResult>(
        "MyMoney.usp_Transaction_GetList", p, ct);

    return new GetTransactionsDbResult
    {
        Items      = items,
        TotalCount = p.Get<int>("@TotalCount")
    };
}
```

### Multi-result set query

```csharp
public async Task<DashboardDbResult> GetDashboardDataAsync(long userId, CancellationToken ct)
{
    var p = new DynamicParameters();
    p.Add("@UserId", userId, DbType.Int64);

    return await db.QueryMultipleAsync(
        "MyMoney.usp_Dashboard_GetStats",
        async multi =>
        {
            var summary = await multi.ReadFirstOrDefaultAsync<DashboardSummaryDbResult>()
                          ?? new DashboardSummaryDbResult();
            var monthly = (await multi.ReadAsync<MonthlySummaryDbResult>()).AsList();

            return new DashboardDbResult
            {
                Summary = summary,
                Monthly = monthly
            };
        },
        p, ct);
}
```

### Execute with output parameter

```csharp
public async Task<bool> ExistsAsync(long userId, string name, CancellationToken ct)
{
    var p = new DynamicParameters();
    p.Add("@UserId", userId, DbType.Int64);
    p.Add("@Name",   name,   DbType.String);
    p.Add("@Exists", dbType: DbType.Boolean, direction: ParameterDirection.Output);

    await db.ExecuteAsync("MyMoney.usp_MyFeature_Exists", p, ct);

    return p.Get<bool>("@Exists");
}
```

### Key repository rules

- **Always specify `DbType` explicitly** for every parameter.
- **Always cast enum values** to their underlying primitive type before passing to `DynamicParameters`.
- **Stored procedure names follow** `"MyMoney.usp_{Feature}_{Action}"` exactly.
- **Repositories never contain business logic.**
- **Return DB model types only** — never DTOs or domain entities.
- **Null parameters** are passed as `(object?)null` — Dapper sends `DBNull.Value`.

---

## 7. Validation Pattern

### Structure

```csharp
public sealed class CreateTransactionValidator : AbstractValidator<CreateTransactionRequest>
{
    public CreateTransactionValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Transaction.AmountMustBePositive);

        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
            .WithMessage(MessageKeys.Transaction.CategoryRequired);

        RuleFor(x => x.TransactionDate)
            .NotEmpty()
            .WithMessage(MessageKeys.Transaction.DateRequired)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage(MessageKeys.Transaction.DateCannotBeFuture);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage(MessageKeys.Transaction.DescriptionTooLong)
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
```

### Applying to an endpoint

```csharp
group.MapPost("create", async (
    CreateTransactionRequest request,
    ITransactionService      service,
    CancellationToken        ct) =>
{
    var result = await service.CreateAsync(request, ct);
    return result.ToHttpResponse();
})
.AddEndpointFilter<ValidationFilter<CreateTransactionRequest>>()
.RequireAuthorization();
```

### Key validation rules

- `WithMessage()` values **must always** be `MessageKeys.*` constants.
- Do **not** add `ValidationFilter<T>` to endpoints that take no request body.
- Business-level validation (e.g., "category not found") belongs in the **service**, not the validator.
- Structural validation (required, length, range, format) belongs in the **validator**.

---

## 8. Mapping Pattern

### Simple flat mapping

```csharp
var response = dbResult
    .Select(r => new TransactionResponse(
        r.TransactionId,
        r.CategoryName,
        r.Amount,
        r.TransactionDate,
        r.Description))
    .ToList();
```

### Mapping with asset URL resolution

```csharp
var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

var response = dbResult
    .Select(r => new CategoryResponse(
        r.CategoryId,
        r.Name,
        r.NameAr,
        BuildIconUrl(storageUtility, r.IconFileName, baseUrl)))
    .ToList();

private static string? BuildIconUrl(IStorageUtility storage, string? fileName, string baseUrl)
{
    if (string.IsNullOrEmpty(fileName)) return null;
    var (url, _) = storage.BuildFilePathWithExpiration(
        FolderPaths.IconsFolder, fileName, isInternalStorage: true, baseUrl: baseUrl);
    return url;
}
```

### Mapping with dictionary joins (multi-result sets)

```csharp
var categoryNameById = dbResult.Categories.ToDictionary(c => c.CategoryId, c => c.Name);

var response = dbResult.Transactions.Select(t =>
{
    categoryNameById.TryGetValue(t.CategoryId, out var categoryName);

    return new TransactionListItemResponse(
        t.TransactionId,
        categoryName ?? string.Empty,
        t.Amount,
        t.TransactionDate);
}).ToList();
```

### Key mapping rules

- **No AutoMapper.** Manual projections only.
- **DB model types stay in the service.** Never leak them to endpoint lambdas.
- **Cast to `IReadOnlyList<T>`** before caching a projected `.ToList()`.
- **File URLs are always built through `IStorageUtility`**, never constructed inline.

---

## 9. Background Job Pattern

### Defining a job payload

Payload records live in `Application/Features/{Feature}/Jobs/`:

```csharp
// Application/Features/Authentication/Jobs/PasswordResetEmailPayload.cs
public record PasswordResetEmailPayload(
    string RecipientEmail,
    string DisplayName,
    string ResetToken,
    DateTime ExpiresAtUtc
);
```

### Enqueueing a job from a service

```csharp
// Inside a service method — inject IBackgroundJobService
public async Task<ServiceResult<object?>> RequestPasswordResetAsync(
    ForgotPasswordRequest request,
    CancellationToken     ct)
{
    // Always return OK regardless of whether the email exists (prevents enumeration)
    var user = await authRepository.GetByEmailAsync(request.Email, ct);

    if (user is not null)
    {
        var token   = tokenHasher.GenerateToken();    // raw random token
        var hashed  = tokenHasher.Hash(token);        // stored hash

        await backgroundJobService.EnqueueAsync(
            jobType:     JobTypes.PasswordResetEmail,
            payload:     new PasswordResetEmailPayload(
                             user.Email,
                             user.DisplayName,
                             token,           // raw token goes in the payload, not the DB
                             DateTime.UtcNow.AddMinutes(30)),
            priority:    BackgroundJobPriority.High,
            ct:          ct);
    }

    return ServiceResultFactory.Success<object?>(
        null,
        InternalResponseCodes.OK,
        await messageProvider.GetMessagesAsync(MessageKeys.Authentication.ResetEmailSent, ct));
}
```

### Implementing a job handler

Job handlers live in `Infrastructure/Jobs/Handlers/`:

```csharp
// Infrastructure/Jobs/Handlers/PasswordResetEmailHandler.cs
internal sealed class PasswordResetEmailHandler(
    IEmailSender   emailSender,
    ILogger<PasswordResetEmailHandler> logger) : IJobHandler<PasswordResetEmailPayload>
{
    public async Task HandleAsync(PasswordResetEmailPayload payload, CancellationToken ct)
    {
        await emailSender.SendAsync(
            to:      payload.RecipientEmail,
            subject: "Reset your password",
            body:    BuildEmailBody(payload));

        logger.LogInformation(
            "Password reset email sent to {Email}", payload.RecipientEmail);
    }

    private static string BuildEmailBody(PasswordResetEmailPayload p)
        => $"Use token {p.ResetToken} before {p.ExpiresAtUtc:u}. ...";
}
```

### Registering job handlers

```csharp
// InfrastructureRegistration.cs
services.AddScoped<IJobHandler<PasswordResetEmailPayload>, PasswordResetEmailHandler>();
services.AddScoped<IJobHandler<WelcomeEmailPayload>,       WelcomeEmailHandler>();
// Register all handlers here — never in Program.cs
```

### Job type constants

```csharp
// Application/Common/Constants/JobTypes.cs
public static class JobTypes
{
    public const string PasswordResetEmail = "PasswordResetEmail";
    public const string WelcomeEmail       = "WelcomeEmail";
    public const string MonthlyReport      = "MonthlyReport";
}
```

### Key background job rules

- **Never use `Task.Run` or fire-and-forget from services.** All async work goes through `IBackgroundJobService.EnqueueAsync`.
- **Each job type has exactly one handler.** The processor looks up the handler by type name.
- **Handlers are idempotent where possible.** The retry mechanism may invoke a handler more than once.
- **Raw tokens are never stored in the database directly.** Include them in the job payload (which is stored in `NVARCHAR(MAX)` and encrypted at rest by SQL Server TDE if enabled).
- **Job type string constants live in `Application/Common/Constants/JobTypes.cs`**, not inline in service code.

---

## 10. Naming Conventions

### Files and types

| Artifact | Pattern | Example |
|---|---|---|
| Request DTO | `{Action}Request` record | `CreateTransactionRequest` |
| Response DTO | `{Action}Response` record | `TransactionResponse` |
| DB input model | `{Action}DbModel` class | `CreateTransactionDbModel` |
| DB result model | `{Action}DbResult` class | `TransactionDbResult` |
| Multi-set DB result | `Get{Data}DbResult` with list properties | `GetTransactionsDbResult` |
| Service interface | `I{Feature}Service` | `ITransactionService` |
| Service class | `{Feature}Service` | `TransactionService` |
| Repository interface | `I{Feature}Repository` | `ITransactionRepository` |
| Repository class | `{Feature}Repository` | `TransactionRepository` |
| Validator | `{Request}Validator` | `CreateTransactionValidator` |
| Endpoint class | `{Feature}Endpoints` | `TransactionEndpoints` |
| Endpoint extension | `Map{Feature}Endpoints` | `MapTransactionEndpoints` |
| Job payload | `{JobType}Payload` record | `PasswordResetEmailPayload` |
| Job handler | `{JobType}Handler` | `PasswordResetEmailHandler` |

### Stored procedures

```
MyMoney.usp_{Feature}_{Action}

Examples:
  MyMoney.usp_Authentication_Register
  MyMoney.usp_Authentication_Login
  MyMoney.usp_Authentication_RefreshToken
  MyMoney.usp_Authentication_RevokeToken
  MyMoney.usp_Dashboard_GetStats
  MyMoney.usp_Transaction_Create
  MyMoney.usp_Transaction_GetList
  MyMoney.usp_Transaction_GetById
  MyMoney.usp_Transaction_Update
  MyMoney.usp_Transaction_Delete
  MyMoney.usp_Category_GetList
  MyMoney.usp_Profile_Get
  MyMoney.usp_Profile_Update
  MyMoney.usp_Profile_ChangePassword
  MyMoney.usp_BackgroundJob_Enqueue
  MyMoney.usp_BackgroundJob_PickUp
  MyMoney.usp_BackgroundJob_Complete
  MyMoney.usp_BackgroundJob_Fail
```

### Cache keys

Lowercase, colon-separated. Include variant parameters as suffix segments.

```
"categories:income:{language}"
"categories:expense:{language}"
"categories:all:{language}"
"localization:messages"
"localization:labels"
```

### Endpoint routes

```
api/auth/register
api/auth/login
api/auth/refresh-token
api/auth/revoke-token
api/auth/forgot-password
api/auth/reset-password
api/dashboard/get/stats
api/transactions/create
api/transactions/get/list
api/transactions/get/{id}
api/transactions/update/{id}
api/transactions/delete/{id}
api/categories/get/list
api/profile/get
api/profile/update
api/profile/change-password
```

All route segments use **kebab-case**.

---

## 11. Coding Conventions

### Language and framework

- **C# 12** — primary constructors, collection expressions (`[.. ]`), `Random.Shared`.
- **.NET 10** target framework.
- Nullable reference types enabled. Use `= null!` for required strings initialized by Dapper; `?` for truly optional fields.

### Class modifiers

```csharp
internal sealed class MyFeatureService(...) : IMyFeatureService { }
internal sealed class MyFeatureRepository(...) : IMyFeatureRepository { }
internal sealed class PasswordResetEmailHandler(...) : IJobHandler<PasswordResetEmailPayload> { }
public static class MyFeatureEndpoints { }
public sealed class CreateMyItemValidator : AbstractValidator<CreateMyItemRequest> { }
```

### Constructor injection

Always use primary constructors. No field assignments in the constructor body.

```csharp
// Correct
internal sealed class TransactionService(
    ITransactionRepository transactionRepository,
    IUserContext           userContext,
    ICacheService          cacheService,
    IMessageProvider       messageProvider) : ITransactionService
{ }
```

### Cancellation tokens

Every async method that reaches the database or external services accepts and passes `CancellationToken ct`.

### Record types for DTOs

```csharp
// Positional record (preferred for small types)
public record CreateTransactionRequest(decimal Amount, int CategoryId, DateOnly TransactionDate);

// Init-only record (preferred when fields need attributes)
public sealed record TransactionResponse
{
    public long    TransactionId   { get; init; }
    public string  CategoryName    { get; init; } = string.Empty;
    public decimal Amount          { get; init; }
    public DateOnly TransactionDate { get; init; }
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description     { get; init; }
}
```

### DynamicParameters alignment

Column-align `DynamicParameters.Add()` calls for readability:

```csharp
var p = new DynamicParameters();
p.Add("@UserId",          model.UserId,                  DbType.Int64);
p.Add("@CategoryId",      model.CategoryId,              DbType.Int32);
p.Add("@TypeId",          (byte)model.TransactionTypeId, DbType.Byte);
p.Add("@Amount",          model.Amount,                  DbType.Decimal);
p.Add("@TransactionDate", model.TransactionDate,         DbType.Date);
p.Add("@Description",     model.Description,             DbType.String);
```

### Comments

Write comments only when the **why** is non-obvious — a hidden constraint, a subtle invariant, or a non-obvious workaround.

```csharp
// Good — explains a security invariant
// Always return OK even if the email doesn't exist — prevents user enumeration
return ServiceResultFactory.Success<object?>(null, InternalResponseCodes.OK, message);

// Good — explains a sentinel value
// NULL: SP returned no rows — user not found or email/password mismatch
if (result is null) return ServiceResultFactory.Failure<LoginResponse>(...);

// Bad — restates what the code does
// Map database results to response objects
var response = dbResult.Select(r => new TransactionResponse(r.Id, r.Amount)).ToList();
```

---

## 12. Common Pitfalls

### 1. Omitting `DbType` from `DynamicParameters`

```csharp
// Wrong
p.Add("@TypeId", model.TransactionTypeId);

// Correct
p.Add("@TypeId", (byte)model.TransactionTypeId, DbType.Byte);
```

### 2. Caching with mismatched types

```csharp
// Wrong — stores List<T>, retrieves IReadOnlyList<T>, always a cache miss
var list = dbResult.Select(...).ToList();
await cacheService.SetAsync(cacheKey, list);
var cached = await cacheService.GetAsync<IReadOnlyList<CategoryResponse>>(cacheKey); // null every time

// Correct
var list = dbResult.Select(...).ToList();
await cacheService.SetAsync(cacheKey, (IReadOnlyList<CategoryResponse>)list);
var cached = await cacheService.GetAsync<IReadOnlyList<CategoryResponse>>(cacheKey); // works
```

### 3. Inline error strings in validators

```csharp
// Wrong
RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be positive.");

// Correct
RuleFor(x => x.Amount).GreaterThan(0).WithMessage(MessageKeys.Transaction.AmountMustBePositive);
```

### 4. Throwing domain exceptions for business failures

```csharp
// Wrong — NotFoundException is for programmer-level contract violations
if (result is null) throw new NotFoundException();

// Correct — "user not found" is a normal business outcome at login
if (result is null)
    return ServiceResultFactory.Failure<LoginResponse>(
        InternalResponseCodes.Unauthorized,
        await messageProvider.GetMessagesAsync(MessageKeys.Authentication.InvalidCredentials, ct));
```

### 5. Using `Results.*` directly in endpoints

```csharp
// Wrong
return Results.Ok(new { data = transactions });

// Correct
var result = await service.GetListAsync(request, ct);
return result.ToHttpResponse();
```

### 6. Fire-and-forget for async work

```csharp
// Wrong — loses durability; not retried if the process crashes
_ = emailSender.SendPasswordResetAsync(email, token);

// Correct — durable, retryable, auditable
await backgroundJobService.EnqueueAsync(
    JobTypes.PasswordResetEmail,
    new PasswordResetEmailPayload(email, name, token, expiry),
    BackgroundJobPriority.High, ct);
```

### 7. Storing raw tokens in the database

```csharp
// Wrong — a DB breach yields usable tokens
p.Add("@Token", rawToken, DbType.String);

// Correct — store only the SHA-256 hash
p.Add("@Token", tokenHasher.Hash(rawToken), DbType.String);
```

### 8. Missing service or repository registration

Every new service must be added to `ServiceCollectionExtensions.AddApplication()`. Every new repository must be added to `InfrastructureRegistration.AddInfrastructure()`. The app compiles fine but throws `InvalidOperationException` at first use.

### 9. Returning `ServiceResult` from a repository

```csharp
// Wrong — mixes concerns
public async Task<ServiceResult<TransactionDbResult>> GetByIdAsync(...) { ... }

// Correct — repositories return DB model types only
public async Task<TransactionDbResult?> GetByIdAsync(...) { ... }
```

### 10. Building file URLs inline in services

```csharp
// Wrong
var url = $"{baseUrl}/uploads/profiles/{user.ProfilePicture}";

// Correct
var (url, _) = storageUtility.BuildFilePathWithExpiration(
    FolderPaths.UserUploads, user.ProfilePicture, isInternalStorage: true, baseUrl: baseUrl);
```

---

## 13. Codebase Examples

### Example: Transaction listing with pagination and filters

**DB Model** (`Application/Interfaces/Repositories/DbModels/Transaction/TransactionDbModels.cs`):
```csharp
public class GetTransactionsDbModel
{
    public long      UserId      { get; set; }
    public byte?     TypeId      { get; set; }     // null = both
    public int?      CategoryId  { get; set; }
    public DateOnly? DateFrom    { get; set; }
    public DateOnly? DateTo      { get; set; }
    public decimal?  AmountMin   { get; set; }
    public decimal?  AmountMax   { get; set; }
    public string    SortBy      { get; set; } = "TransactionDate";
    public string    SortDir     { get; set; } = "DESC";
    public int       PageNumber  { get; set; } = 1;
    public int       PageSize    { get; set; } = 20;
}

public class TransactionDbResult
{
    public long     TransactionId   { get; set; }
    public string   CategoryName    { get; set; } = null!;
    public string   CategoryNameAr  { get; set; } = null!;
    public byte     TypeId          { get; set; }
    public decimal  Amount          { get; set; }
    public string?  Description     { get; set; }
    public DateOnly TransactionDate { get; set; }
}

public class GetTransactionsDbResult
{
    public IReadOnlyList<TransactionDbResult> Items      { get; set; } = [];
    public int                                TotalCount { get; set; }
}
```

**Service** (maps filters, calls repo, projects response):
```csharp
public async Task<ServiceResult<PagedResponse<TransactionListItemResponse>>> GetListAsync(
    GetTransactionsRequest request,
    CancellationToken      ct)
{
    var dbModel = new GetTransactionsDbModel
    {
        UserId     = userContext.UserId,
        TypeId     = request.TypeId.HasValue ? (byte?)request.TypeId : null,
        CategoryId = request.CategoryId,
        DateFrom   = request.DateFrom,
        DateTo     = request.DateTo,
        AmountMin  = request.AmountMin,
        AmountMax  = request.AmountMax,
        SortBy     = request.SortBy ?? "TransactionDate",
        SortDir    = request.SortDir ?? "DESC",
        PageNumber = request.PageNumber,
        PageSize   = request.PageSize
    };

    var dbResult = await transactionRepository.GetListAsync(dbModel, ct);

    var language = userContext.Language;

    var items = dbResult.Items
        .Select(r => new TransactionListItemResponse(
            r.TransactionId,
            language == SystemLanguages.Arabic ? r.CategoryNameAr : r.CategoryName,
            r.TypeId,
            r.Amount,
            r.Description,
            r.TransactionDate))
        .ToList();

    var response = new PagedResponse<TransactionListItemResponse>(
        Items:      items,
        TotalCount: dbResult.TotalCount,
        PageNumber: request.PageNumber,
        PageSize:   request.PageSize);

    return ServiceResultFactory.Success(
        response,
        InternalResponseCodes.OK,
        await messageProvider.GetMessagesAsync(MessageKeys.Common.Success, ct));
}
```

**Endpoint**:
```csharp
public static class TransactionEndpoints
{
    public static void MapTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/transactions")
                       .WithTags("Transactions")
                       .RequireAuthorization();

        group.MapPost("get/list", async (
            GetTransactionsRequest  request,
            ITransactionService     service,
            CancellationToken       ct) =>
        {
            var result = await service.GetListAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<GetTransactionsRequest>>();

        group.MapPost("create", async (
            CreateTransactionRequest request,
            ITransactionService      service,
            CancellationToken        ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.ToHttpResponse();
        })
        .AddEndpointFilter<ValidationFilter<CreateTransactionRequest>>();
    }
}
```

---

### Example: Refresh token rotation

**Service** (`AuthService.RefreshTokenAsync`):
```csharp
public async Task<ServiceResult<AuthTokenResponse>> RefreshTokenAsync(
    string            rawRefreshToken,
    string            ipAddress,
    CancellationToken ct)
{
    var tokenHash = tokenHasher.Hash(rawRefreshToken);
    var storedToken = await authRepository.GetRefreshTokenAsync(tokenHash, ct);

    if (storedToken is null || storedToken.RevokedOnUtc.HasValue)
    {
        return ServiceResultFactory.Failure<AuthTokenResponse>(
            InternalResponseCodes.Unauthorized,
            await messageProvider.GetMessagesAsync(MessageKeys.Authentication.InvalidToken, ct));
    }

    if (storedToken.ExpiresOnUtc < DateTime.UtcNow)
    {
        return ServiceResultFactory.Failure<AuthTokenResponse>(
            InternalResponseCodes.Unauthorized,
            await messageProvider.GetMessagesAsync(MessageKeys.Authentication.TokenExpired, ct));
    }

    // Generate new token pair
    var newRawToken  = tokenHasher.GenerateToken();
    var newTokenHash = tokenHasher.Hash(newRawToken);

    var rotateModel = new RotateRefreshTokenDbModel
    {
        OldTokenHash     = tokenHash,
        NewTokenHash     = newTokenHash,
        NewExpiresOnUtc  = DateTime.UtcNow.AddDays(7),
        RevokedByIp      = ipAddress,
        ReplacedByToken  = newTokenHash,
        ReasonRevoked    = "Rotation"
    };

    await authRepository.RotateRefreshTokenAsync(rotateModel, ct);

    var user       = await authRepository.GetByIdAsync(storedToken.UserId, ct);
    var accessToken = jwtService.GenerateAccessToken(new JwtTokenResponse(
        user!.UserId, user.Email, user.DisplayName, [user.RoleId]));

    return ServiceResultFactory.Success(
        new AuthTokenResponse(accessToken, newRawToken),
        InternalResponseCodes.OK,
        await messageProvider.GetMessagesAsync(MessageKeys.Authentication.TokenRefreshed, ct));
}
```

---

*Last updated: 2026-06-28. Update this document whenever a new pattern is introduced or an existing pattern changes.*
