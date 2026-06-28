/* =============================================================================
   Audit remediation H7 — Background-job idempotency / de-duplication
   Target schema : MyMoney DB v12+ (run after db-script-v12 is in place)
   Apply ORDER   : apply this BEFORE deploying the matching .NET change
                   (the new .NET passes @DedupKey for scheduler enqueues).
   Idempotent    : safe to re-run; self-registers in MyMoney.SchemaMigrations.

   What it does
   ------------
   Adds an optional DedupKey to BackgroundJobs and a filtered UNIQUE index that
   permits at most one *non-terminal* (Pending=1 / Processing=2) job per key.
   usp_BackgroundJob_Enqueue gains @DedupKey: when supplied and a matching
   non-terminal job already exists, the insert is skipped — so duplicate
   schedules (e.g. the same hourly tick fired by two instances) collapse to one.
   Completed(3)/Failed(4) rows are excluded from the index, so the same logical
   job can legitimately run again in a later window.
   ============================================================================= */
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('MyMoney.BackgroundJobs') AND name = 'DedupKey')
BEGIN
    ALTER TABLE MyMoney.BackgroundJobs ADD DedupKey NVARCHAR(200) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UX_BackgroundJobs_DedupKey_Active'
                 AND object_id = OBJECT_ID('MyMoney.BackgroundJobs'))
BEGIN
    CREATE UNIQUE INDEX UX_BackgroundJobs_DedupKey_Active
        ON MyMoney.BackgroundJobs (DedupKey)
        WHERE DedupKey IS NOT NULL AND StatusId < 3;
END
GO

ALTER PROCEDURE [MyMoney].[usp_BackgroundJob_Enqueue]
    @JobType     NVARCHAR(200),
    @Payload     NVARCHAR(MAX),
    @Priority    TINYINT       = 2,
    @ScheduledAt DATETIME2(0)  = NULL,
    @MaxAttempts INT           = 3,
    @CreatedBy   BIGINT        = NULL,
    @DedupKey    NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Fast-path skip when a matching non-terminal job is already queued/running.
    IF @DedupKey IS NOT NULL
       AND EXISTS (SELECT 1 FROM MyMoney.BackgroundJobs WITH (UPDLOCK, HOLDLOCK)
                   WHERE DedupKey = @DedupKey AND StatusId < 3)
        RETURN;

    BEGIN TRY
        INSERT INTO MyMoney.BackgroundJobs
            (JobType, Payload, StatusId, Priority, ScheduledAt, MaxAttempts, CreatedAt, CreatedBy, DedupKey)
        VALUES
            (@JobType, @Payload, 1, @Priority,
             ISNULL(@ScheduledAt, GETUTCDATE()),
             @MaxAttempts, GETUTCDATE(), @CreatedBy, @DedupKey);
    END TRY
    BEGIN CATCH
        -- 2601/2627 = unique-index violation: a concurrent enqueue won the race. Treat as success (deduped).
        IF ERROR_NUMBER() NOT IN (2601, 2627)
            THROW;
    END CATCH
END
GO

IF NOT EXISTS (SELECT 1 FROM MyMoney.SchemaMigrations WHERE ScriptName = 'audit-h7-job-dedup.sql')
    INSERT INTO MyMoney.SchemaMigrations (ScriptName, AppliedAtUtc, Notes)
    VALUES ('audit-h7-job-dedup.sql', GETUTCDATE(),
            'H7: BackgroundJobs.DedupKey + filtered unique index + Enqueue @DedupKey');
GO
