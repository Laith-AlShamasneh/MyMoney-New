# MyMoney — Enterprise Backend Architecture Audit

**Date:** 2026-06-27
**Reviewed against:** `DEVELOPMENT_GUIDE.md`, `ARCHITECTURE_DECISIONS.md`, and the workspace-scoped database (v12, source of truth).
**Scope:** Full backend — Shared, Domain, Application, Infrastructure, WebApi (≈385 `.cs` files, ≈26k LOC, 19 feature modules).
**Stance:** Architecture review, not a line-by-line code review. Everything reviewed, challenged, justified. **No code was changed.**

> **Coverage note (honesty):** I read the entire architectural spine (composition root, DI, middleware, filters, exception handling, result/response contracts, data engine, identity/JWT/hashers, background-job engine + schedulers, config) and then sampled representative vertical slices deeply (Authentication, Workspace, Transaction, RecurringTransactions, Currency, CashFlow forecasting, Storage/OCR). Findings about systemic patterns are validated against multiple modules. Module-specific financial-logic correctness (Budget V2, Goals, FIL, Reports generators) was spot-checked, not exhaustively traced; where I flag those I say so.

---

## Executive Summary

The codebase is, on the whole, **well-engineered and unusually disciplined** for its size: Clean Architecture boundaries are respected (no `HttpContext` or Infrastructure leakage into Application, no raw SQL, no EF, consistent `ServiceResult`/`ApiResponse` contracts, consistent validator/endpoint/repository patterns). The team's own guide rules are followed closely. That foundation is genuinely strong.

However, **it is not yet production-grade for commercial SaaS**, and there is one structural issue that dominates everything else:

> **The flagship "Shared Accounts & Workspace Collaboration" platform is non-functional at the API layer.** The database was fully re-scoped to workspaces (every financial stored procedure takes `@WorkspaceId`), but **Phase 3d (the .NET wiring) was never done.** Only `WorkspaceRepository` ever sends `@WorkspaceId`; every other repository calls its SPs without it, so every SP resolves to the caller's *personal* workspace. Switching into a shared workspace changes nothing — members cannot see or post transactions, budgets, goals, receipts, or reports in it. The feature ships but does nothing. It does, at least, **fail safe** (no cross-tenant data leakage), because NULL → personal workspace, and personal workspace == the user's own historical data.

Beyond that, the highest-impact gaps are **operational/security hardening that any SaaS needs but this app currently lacks**: committed production secrets, 12-hour access tokens, no rate limiting, sensitive files served anonymously, background schedulers that double-fire under horizontal scale, no health checks, no API docs, and full request bodies (financial PII) written to a log database.

**Verdict:** Excellent bones, unfinished multi-tenancy, and missing the operational hardening layer. With the Critical + High items below addressed, this becomes a credible enterprise platform.

### Findings by severity

| Severity | Count | Theme |
|---|---|---|
| **Critical** | 5 | Multi-tenancy not wired, committed secrets, long-lived tokens, anonymous file access, no rate limiting |
| **High** | 8 | PII in logs, scheduler double-fire at scale, silent currency-conversion zeroing, finalize migration not applied, no health checks, request-logging before auth, no idempotency on jobs, weak token-revocation story |
| **Medium** | 11 | No API versioning/Swagger, sync connection open, sequential job processing, rounding inconsistency, dead Domain entities, config validation, CORS/headers, etc. |
| **Low** | 9 | Doc drift, naming, magic numbers, BCrypt work factor, empty catches, etc. |

---

## CRITICAL

### C1 — Workspace multi-tenancy is not wired into the application layer (Phase 3d missing)
- **Problem:** The DB expects `@WorkspaceId` on all ~25 scoped financial SPs; the app never passes it (only `WorkspaceRepository` references `@WorkspaceId` — verified by grep across all of `Infrastructure/Services`). There is no per-request "current workspace" resolution anywhere in the pipeline (no `IWorkspaceContext`, nothing reading `UserWorkspacePreferences.CurrentWorkspaceId`). `TransactionRepository`, `BudgetRepository`, `GoalRepository`, `ReceiptRepository`, `CashFlowForecastRepository`, `FinancialIntelligenceRepository`, `ReportRepository`, etc. all call their SPs with `@UserId` only.
- **Root cause:** DB remediation (Phases 1–3c) completed; the corresponding backend wiring (3d) was scoped but never implemented.
- **Business impact:** The entire paid differentiator — shared family/company/accountant workspaces — does not work. A member who accepts an invitation and switches workspace still operates only on their own personal ledger. This is the headline feature of the most recent release.
- **Technical impact:** DB and app are contractually out of sync. Background services (FIL, CashFlow) still iterate *users*, not *workspaces*, so analytics for shared workspaces are never computed. The pending `finalize` migration (WorkspaceId → NOT NULL on 11 analytics tables) **must not be applied** until this is done, or legacy NULL-inserting callers will trip the guard.
- **Recommended solution:** Implement 3d: (1) add a scoped `IWorkspaceContext` that resolves the active workspace from `usp_Workspace_GetCurrentContext` (cache per request); (2) thread `@WorkspaceId` through every scoped DbModel + repository call (param sits right after `@UserId`, NULL = personal = back-compat); (3) update FIL/CashFlow schedulers to iterate workspaces (`usp_FIL_User_GetActive` / cashflow `GetActiveUsers` now return WorkspaceId/OwnerUserId); (4) honor changed result codes (`RefreshToken` GetById signature, `Transaction_CreateWithCurrency` code 3, `GoalContribution_Add` code −4); (5) only then run `finalize v3` + `phase5 indexes`.
- **Complexity:** High (touches every financial module; mechanical but broad).
- **Breaking:** Non-breaking to existing personal-scope clients (NULL preserves today's behavior); enabling shared scope is additive.

### C2 — Production secrets committed in `appsettings.json`
- **Problem:** `WebApi/appsettings.json` contains a live JWT signing key (`Jwt:SecretKey`), a real Gmail SMTP username + app password (`fanyxxxvmbdsczpq`), and connection strings — all in source control.
- **Root cause:** No secrets-management strategy; dev defaults checked in.
- **Business impact:** Anyone with repo access (or a leaked clone) can forge JWTs for *any* user (full account takeover across the platform), send mail as the company, and learn DB topology. For a financial product this is a catastrophic disclosure.
- **Technical impact:** Key rotation requires a redeploy; the symmetric JWT key being public means the entire auth system is compromised.
- **Recommended solution:** Move all secrets to environment variables / user-secrets (dev) / a secret store (Azure Key Vault, AWS Secrets Manager) in prod. Rotate the JWT key and the Gmail credential immediately (treat both as burned). Add config validation that fails startup if secrets come from the committed file in Production. Purge secrets from git history.
- **Complexity:** Low–Medium.
- **Breaking:** Non-breaking (config plumbing only).

### C3 — Access tokens live 12 hours, defeating the short-lived-token model
- **Problem:** `Jwt:ExpiryHours = 12` and `JwtService` does `DateTime.UtcNow.AddHours(_options.ExpiryHours)`. `ARCHITECTURE_DECISIONS.md` states access tokens are "short-lived JWTs (default 15 minutes)". The implementation issues 12-hour tokens.
- **Root cause:** Config value and unit (hours, not minutes) diverged from the documented design.
- **Business impact:** A stolen/leaked access token is valid for 12 hours with **no revocation path** (JWTs are stateless; only refresh tokens are revocable). Logout and password-change revoke refresh tokens but cannot invalidate an outstanding access token. This is the difference between a 15-minute and a 12-hour breach window on a finance API.
- **Technical impact:** The refresh-token rotation machinery exists but provides little security benefit when the access token itself is long-lived.
- **Recommended solution:** Set access-token lifetime to ~15 minutes (switch the option to minutes for clarity), rely on refresh rotation for continuity. Optionally add a `jti`/token-version deny-list or a `SecurityStamp` claim checked against the user record for true revocation on logout/password-change.
- **Complexity:** Low (config + minor option rename); Medium if adding revocation.
- **Breaking:** Non-breaking (clients already refresh).

### C4 — User-uploaded receipts and generated reports are served by anonymous static-file middleware
- **Problem:** `app.UseStaticFiles()` exposes all of `wwwroot/uploads/`, which includes `uploads/receipts/` (receipt images = financial PII) and `uploads/reports/` (generated financial statements). `StorageUtility` builds public URLs with `TimeSpan.MaxValue` (no expiry) and no signature/auth. Access control is "GUID filename is hard to guess" only.
- **Root cause:** Storage decision optimized for simplicity; sensitive artifacts placed under the publicly-served web root.
- **Business impact:** Broken access control on private financial documents. A leaked, logged, shared, or brute-forced URL exposes another customer's receipts/reports with no authentication. Likely violates GDPR/data-protection commitments for a finance SaaS.
- **Technical impact:** No per-object authorization; no expiry; CDN/proxy caching could amplify exposure.
- **Recommended solution:** Move receipts/reports out of `wwwroot`. Serve them through an authenticated endpoint (`/api/receipts/file/{id}`) that verifies ownership/workspace membership via `IFileService.DownloadAsync` and streams the bytes. Keep only truly public assets (category icons) under static files. If keeping static serving, gate the folder with auth middleware and signed, expiring URLs.
- **Complexity:** Medium.
- **Breaking:** Breaking for any client that hot-links the current public URLs (acceptable, and necessary).

### C5 — No rate limiting anywhere (auth endpoints especially)
- **Problem:** No `AddRateLimiter`/`UseRateLimiter`, no per-IP/per-account throttling. `login`, `forgot-password`, `resend-confirmation`, `refresh-token`, `register`, and invitation-token endpoints are all unbounded.
- **Root cause:** Resilience layer not yet added.
- **Business impact:** Password brute-forcing (account lockout helps per-account but not credential stuffing across many accounts), email-bomb via forgot-password/resend (also burns the SMTP quota), invitation-token guessing, and general DoS. BCrypt verify is CPU-expensive, so unbounded login attempts are also a cheap CPU-exhaustion vector.
- **Technical impact:** No first line of defense before the (expensive) service/DB work runs.
- **Recommended solution:** Add ASP.NET Core rate limiting: strict fixed/sliding window on auth endpoints (per IP + per email), a global per-IP limiter for the rest, and tighter limits on email-sending endpoints. Pair with the lockout that already exists.
- **Complexity:** Low–Medium.
- **Breaking:** Non-breaking.

---

## HIGH

### H1 — Full request bodies (financial PII) written to a SQL log sink
- **Problem:** `RequestLoggingMiddleware` buffers and logs the raw request body for all non-auth endpoints; the Serilog `MSSqlServer` sink persists `RequestBody` as `nvarchar(max)` in `MyMoneyLogs`. Transaction amounts, descriptions, notes, category data, profile data, etc. land in a second database in plaintext, indefinitely.
- **Business impact:** PII/financial data sprawl into a logging store with its own (separate, also-committed) connection string; expands breach surface and complicates GDPR data-subject/erasure requests.
- **Recommended solution:** Log metadata, not bodies (or hash/redact). If body logging is needed for debugging, gate it behind a non-Production flag, cap retention, and exclude financial endpoints. Add a retention/purge job for the log DB.
- **Complexity:** Low. **Breaking:** No.

### H2 — `RequestLoggingMiddleware` runs before authentication → `UserId` always 0
- **Problem:** In `Program.cs`, `UseMiddleware<RequestLoggingMiddleware>()` is registered **before** `UseAuthentication()`. `IUserContext.UserId` reads JWT claims, which aren't populated yet, so every logged `UserId` is `0`. The documented `CorrelationIdMiddleware` doesn't exist (only `RequestLoggingMiddleware` is present), so there's also no correlation ID despite the guide describing one.
- **Business impact:** Audit/forensic logs cannot attribute actions to users — a serious gap for a financial system and for incident response.
- **Recommended solution:** Move request logging after `UseAuthentication`/`UseAuthorization`, or enrich `UserId` later in the pipeline. Implement the `CorrelationIdMiddleware` the docs promise (X-Correlation-Id in/out + `LogContext`). Reconcile docs with reality.
- **Complexity:** Low. **Breaking:** No.

### H3 — Background schedulers double-fire under horizontal scale
- **Problem:** All hosted schedulers (`FILSchedulerService`, `CashFlow`, `Budget`, `Goal`, `Recurring`, `Calendar`, `Currency`) and `BackgroundJobProcessor` run in **every** app instance and track "last run" in in-memory fields. With 2+ instances (the stated scaling goal), each instance independently enqueues the hourly/daily/monthly jobs → duplicate work, duplicate notifications, duplicate analytics recomputes. On any restart, the hourly tick fires immediately (`_lastHourlyRun = MinValue`).
- **Business impact:** Duplicate emails/notifications to customers, doubled compute cost, and potential double-counting in any non-idempotent job. Blocks safe horizontal scaling.
- **Recommended solution:** Add leader election / a distributed lock (e.g., a DB-based advisory lock or a singleton "scheduler" deployment) so only one instance schedules. Make enqueue idempotent with a natural key (see H7). Persist last-run state in the DB, not memory.
- **Complexity:** Medium. **Breaking:** No.

### H4 — Currency conversion silently returns 0 on a missing rate
- **Problem:** `CurrencyConversionService.FailedResult` returns a `ConversionResult` with `ConvertedAmount = 0m` and `ExchangeRate = 0m` and no explicit success flag. A caller that doesn't inspect for the failure sentinel will treat a missing exchange rate as "this is worth 0".
- **Business impact:** Potential silent zeroing of monetary values in multi-currency aggregation/conversion — incorrect balances/reports. High financial-correctness risk depending on call sites.
- **Technical impact:** Failure is indistinguishable from a legitimate result except by `Amount==0 && Rate==0`.
- **Recommended solution:** Add an explicit `Succeeded`/`Status` member to `ConversionResult`; make callers handle failure (skip, surface an error, or use a fallback). Audit all call sites of `ConvertAsync`/`ConvertBatchAsync` for the 0-amount assumption.
- **Complexity:** Low–Medium. **Breaking:** Internal contract change only.

### H5 — No health checks / readiness probes
- **Problem:** No `MapHealthChecks`, no DB/SMTP/liveness probes.
- **Business impact:** Cannot run safely behind a load balancer / Kubernetes; no automated detection of a dead DB connection or stuck instance.
- **Recommended solution:** Add `AddHealthChecks()` with SQL Server + (optional) SMTP checks; expose `/health/live` and `/health/ready`.
- **Complexity:** Low. **Breaking:** No.

### H6 — Pending `finalize` migration leaves analytics tables nullable / inconsistent
- **Problem (DB, but app-coupled):** Per prior remediation notes, `WorkspaceId` is NOT NULL on 14 ledger tables but still NULLABLE on 11 analytics tables; the `finalize v3` and `phase5 index` scripts are not yet applied. Until C1 is done, applying finalize would break legacy callers.
- **Business impact:** Schema is in a half-migrated state; the over-indexed UserId-leading indexes (≈28 dead indexes) still cost write throughput and storage.
- **Recommended solution:** Sequence is fixed: do C1 (3d wiring) → deploy → run `finalize v3` → run `phase5 indexes` → re-validate with Query Store after ~1–2 weeks.
- **Complexity:** Medium (coordination). **Breaking:** Internal.

### H7 — Background jobs have no idempotency / dedup key
- **Problem:** `IBackgroundJobService.EnqueueAsync` always inserts a new row; handlers are "idempotent where possible" by convention only. Combined with H3 (double scheduling) and at-least-once retry semantics, the same logical job can run multiple times.
- **Business impact:** Duplicate emails, notifications, and recomputes; in any handler that mutates money/state non-idempotently, duplicated effects.
- **Recommended solution:** Add an optional dedup/natural key on enqueue (e.g., `JobType + scope + period`) with a unique filter in the jobs table; make money-affecting handlers idempotent explicitly.
- **Complexity:** Medium. **Breaking:** No.

### H8 — Weak access-token revocation story
- **Problem:** Logout, password-change, and email-change revoke *refresh* tokens but cannot revoke an outstanding *access* token (stateless JWT). Compounded by C3 (12h lifetime).
- **Business impact:** "Log out everywhere" and "change password to kick out an attacker" do not actually terminate active sessions until the access token expires.
- **Recommended solution:** Introduce a per-user `SecurityStamp`/token-version claim validated on each request (cheap cache lookup), bumped on logout-all/password-change; or shorten access tokens (C3) and accept the residual window.
- **Complexity:** Medium. **Breaking:** No (additive claim).

---

## MEDIUM

### M1 — No API versioning
No `Asp.Versioning` / version segment in routes. For a SaaS with mobile/desktop/public-API ambitions, breaking-change management will be painful. Add URL or header versioning before the first external consumer. *Non-breaking now; expensive later.*

### M2 — No OpenAPI/Swagger
No `AddEndpointsApiExplorer`/Swashbuckle. The guide claims "Swagger quality" matters, but there is no API documentation surface at all. Add Swagger (gated to non-Prod or behind auth). Especially important given the "POST-only, body-bound" convention which is non-discoverable. *Medium.*

### M3 — Synchronous connection open
`SqlConnectionFactory.CreateConnection()` calls `connection.Open()` (blocking) on a Scoped path used by every request. Under load this ties up thread-pool threads during connect. Prefer an async-open path (`OpenAsync`) exposed through the executor. *Medium perf.*

### M4 — Background jobs processed strictly sequentially
`BackgroundJobProcessor` picks up a batch then `foreach await`s one job at a time, with no per-job timeout. A single slow job (e.g., SMTP latency) stalls the whole batch and delays time-sensitive jobs. Add bounded parallelism + per-job timeout/cancellation. *Medium.*

### M5 — Inconsistent rounding strategy across financial code
`CurrencyConversionService` uses banker's rounding (`ToEven`, 4 dp); `ForecastEngine` uses `AwayFromZero` (2 dp). Forecasts are estimates so this isn't a ledger bug, but a finance platform should standardize a documented rounding policy and currency-aware decimal places. *Medium (consistency/correctness hygiene).*

### M6 — `double` used in financial projection paths
`ForecastEngine`/`ForecastRiskDetector` cast `decimal` money to `double` for averages/variance/CV. Acceptable for statistical scores, but document the boundary and keep all *ledger* math in `decimal`. Confirm no projected `double` value is ever persisted as an authoritative monetary amount. *Medium.*

### M7 — Dead Domain entities contradict the stated architecture
`Domain/Entities/**` (`User`, `Person`, `Transaction`, `Category`, `RefreshToken`, `Role`, `UserRole`, `BackgroundJob`, `BaseEntity`) are **unreferenced** (grep finds no `Domain.Entities` usage). `ARCHITECTURE_DECISIONS.md` explicitly says Domain holds the exception hierarchy *only*. Either delete them or formally adopt them — right now they're misleading dead code. *Medium maintainability.*

### M8 — No configuration validation / options validation
Options are bound with `Configure<T>` but never validated (`ValidateOnStart`, `ValidateDataAnnotations`). Missing/invalid JWT, SMTP, storage, or job settings fail late and obscurely. Add `ValidateOnStart` and fail fast. *Medium.*

### M9 — Security headers / HSTS / response hardening absent
Only `UseHttpsRedirection`. No HSTS, no `X-Content-Type-Options`, no `Referrer-Policy`, no anti-clickjacking. CORS uses `AllowAnyHeader/AllowAnyMethod` + `AllowCredentials` (origins are explicit, which is OK, but the surface is broad). Add a security-headers middleware and HSTS in Production. *Medium.*

### M10 — Onboarding init failure swallowed during registration
`AuthService.RegisterAsync` does `try { onboarding.InitializeAsync } catch { }` — a user can be created without onboarding state, and nothing is logged. At minimum log the failure (and consider a reconciliation job). *Medium.*

### M11 — Timing-based user enumeration on login
`LoginAsync` returns immediately when the email doesn't exist but runs BCrypt verify when it does, creating a measurable timing oracle despite the identical message. Consider a dummy verify on the not-found path to equalize timing. *Medium (the explicit goal is no enumeration).*

---

## LOW

- **L1 — Doc drift:** `DEVELOPMENT_GUIDE.md`/`ARCHITECTURE_DECISIONS.md` describe a `CorrelationIdMiddleware`, a 15-minute token, and "Domain = exceptions only" — none currently true. Update the constitution to match reality (or fix reality to match it). 
- **L2 — BCrypt work factor 11:** Reasonable but on the low side for 2026; consider 12 and benchmark.
- **L3 — `.Result` on tasks** in `FinancialIntelligenceService` (after `Task.WhenAll`, so safe, but stylistically risky and easy to misread). Prefer awaiting each task or `await Task.WhenAll` then deconstruct.
- **L4 — Magic numbers:** `RefreshTokenExpiryDays = 7`, confidence weights, trend factors, retry counts inline. Centralize in options/constants.
- **L5 — `LocalFileService.GetPhysicalPath`** has no path-canonicalization guard; keys are internally generated (GUIDs) so low risk, but add a `..`/traversal check for defense-in-depth.
- **L6 — OCR is a stub:** `LocalOcrProvider.ExtractAsync` always returns null; receipts are uploaded but never OCR'd. Fine as a placeholder, but the receipt "processing" feature is effectively inert — make sure product/marketing reflects that.
- **L7 — HTTP semantics:** Everything returns HTTP 200 (even Created/NotFound/Unauthorized), with the real status in the envelope; `ValidationFilter` is the lone exception (returns 400). Intentional, but inconsistent and unfriendly to standard HTTP tooling/monitoring. Document it as a deliberate contract.
- **L8 — `IDbExecutor` is Scoped but stateless** (opens/closes a connection per call); could be Singleton. Harmless, minor.
- **L9 — Empty/▸terse catches** in `ReportService` (file delete) and `GenerateReportHandler` (notification) are reasonable but should at least `LogDebug` the swallowed error.

---

## Cross-cutting answers to the phase questions

- **Clean Architecture / SOLID / boundaries:** Strong. No inward-dependency violations found; Application has zero `HttpContext`/Infrastructure references; no raw SQL; contracts are clean. CQRS/MediatR correctly rejected for this scope.
- **Security:** Good primitives (BCrypt, SHA-256 token hashing, refresh rotation, lockout, no-enumeration messaging, hashed reset/confirmation tokens). Undermined by C2/C3/C4/C5 and H1/H2/H8 — i.e., the *operational* security layer, not the crypto.
- **Financial accuracy:** Ledger math lives in audited SPs (decimal); app-side estimate math is reasonable. Watch H4 (silent zeroing) and M5/M6 (rounding/double policy).
- **Performance/scalability:** Fine for thousands of users on a single instance. Blockers for 100k+/horizontal: H3 (scheduler leader election), M3 (sync open), M4 (sequential jobs), in-process cache (acceptable until multi-instance, then needs distributed cache for rate/limits and shared invalidation). The DB index cleanup (phase5) is pending.
- **Enterprise readiness:** Not yet — missing C1 (multi-tenancy), health checks (H5), versioning (M1), API docs (M2), secrets mgmt (C2), rate limiting (C5), proper audit logging (H1/H2). All are well-scoped, additive fixes.

---

## Recommended remediation order (after your approval)

1. **Critical:** C2 (secrets, immediate) → C3 (token lifetime) → C5 (rate limiting) → C4 (file access) → C1 (workspace 3d wiring — largest).
2. **High:** H2/H1 (logging+attribution) → H5 (health) → H4 (currency) → H8 (revocation) → H3/H7 (scheduler+idempotency) → H6 (finalize/index migration, gated on C1).
3. **Medium:** M8/M9 (config+headers) → M1/M2 (versioning+docs) → M3/M4 (perf) → M5/M6 (financial policy) → M7/M10/M11.
4. **Low:** batch cleanup + doc reconciliation.

Each phase can preserve backward compatibility; the only intentionally breaking change is C4 (file URLs), which is required.

*No code has been modified. Awaiting approval to begin Phase 1 (Critical).*
