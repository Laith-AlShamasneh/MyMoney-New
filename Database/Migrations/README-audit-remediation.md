# Audit remediation — DB migration apply order

These cover the three audit findings that need database changes (H6, H7, H8). All
scripts are idempotent and register themselves in `MyMoney.SchemaMigrations`.
Apply on **staging first**, verify, then production.

## H6 — finish the workspace-scoping rollout (scripts already authored)

The workspace-scoping (Phase 3d) is now fully wired in the .NET layer, which unblocks
the two remaining DB scripts (kept on the Desktop with the rest of the phase set):

1. `db-migration-phase3c-08-finalize.sql` (**re-run the v3 version**) — backfills any
   remaining `NULL WorkspaceId`, then `ALTER COLUMN ... NOT NULL` on the 11 analytics
   tables (dropping/recreating their filtered indexes + unique constraints around the
   alter). Must run **after** the workspace-aware backend is deployed.
2. `db-migration-phase5-indexes.sql` — drops the ~28 dead `UserId`-leading indexes on
   workspace-scoped tables and adds the filtered workspace composites. Run **after**
   finalize. Re-validate with Query Store ~1–2 weeks later.

**Order:** deploy workspace-aware backend → `phase3c-08-finalize (v3)` → `phase5-indexes`.

## H7 — background-job de-duplication

`audit-h7-job-dedup.sql` — adds `BackgroundJobs.DedupKey`, a filtered unique index,
and `@DedupKey` on `usp_BackgroundJob_Enqueue`.

**Order:** apply this script **before** deploying the matching .NET change (the new
.NET passes `@DedupKey` on scheduler enqueues). The old `usp_BackgroundJob_Enqueue`
has no `@DedupKey` parameter, so the script must land first.

## H8 — access-token revocation (SecurityStamp)

`audit-h8-security-stamp.sql` — adds `Users.SecurityStamp` plus
`usp_Authentication_GetSecurityStamp` / `usp_Authentication_BumpSecurityStamp`.

The .NET side is **gated behind `Jwt:ValidateSecurityStamp` (default `false`)**, so it
can ship independently:

**Order:** deploy .NET (flag `false`, no DB dependency) → apply this script →
set `Jwt:ValidateSecurityStamp = true`. From then on, a password change (or a future
"log out everywhere") bumps the stamp and invalidates that user's outstanding access
tokens within the validation cache TTL (~60s; the API also evicts the cache entry on
bump for immediate effect).

## Recommended overall sequence

```
deploy workspace-aware backend (already done in code)
  → phase3c-08-finalize (v3)
  → phase5-indexes
apply audit-h7-job-dedup.sql  → deploy .NET with H7 enqueue wiring
deploy .NET with H8 wiring (flag off)
  → apply audit-h8-security-stamp.sql
  → set Jwt:ValidateSecurityStamp = true
```
