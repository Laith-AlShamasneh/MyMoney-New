-- =============================================================================
-- Migration: 011_FIS_CriticalFixes
-- Description: Resolves three Critical-severity findings from the FIS audit.
--
--   C3 — Category-aware insight deduplication
--        ALTER: usp_FIL_Insight_ExistsForMonth
--        Adds optional @CategoryId so SpendingSpike / OverspendingAlert insights
--        are deduped per (Code + CategoryId) rather than per Code alone.
--
--   C4 — Database recovery mode (RECOVERY SIMPLE → FULL)
--        REQUIRES immediate full backup after STEP 1.
--        REQUIRES a scheduled transaction-log backup job after STEP 3.
--        Read all comments before executing.
--
--   C5 — Large-transaction detection for new users
--        ALTER: usp_FIL_Transaction_GetLargeRecent
--        Switches to LEFT JOIN so users with no 3-month expense baseline are
--        included via an absolute-minimum threshold (@AbsoluteMin).
--
-- Author: Laith Al-Shamasneh
-- Date:   2026-06-17
-- =============================================================================


-- =============================================================================
-- C3 — Fix: usp_FIL_Insight_ExistsForMonth
-- =============================================================================
-- When @CategoryId IS NULL  → matches any insight with this Code this month
--   (used for non-category-scoped insights such as UnusualTransaction)
-- When @CategoryId IS NOT NULL → matches only insights with this Code + Category
--   (used for category-scoped insights: SpendingSpike, OverspendingAlert, etc.)
-- =============================================================================

ALTER PROCEDURE [MyMoney].[usp_FIL_Insight_ExistsForMonth]
    @UserId     BIGINT,
    @Code       NVARCHAR(50),
    @Year       INT,
    @Month      INT,
    @CategoryId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CASE WHEN EXISTS (
        SELECT 1
        FROM   [MyMoney].[FinancialInsights]
        WHERE  [UserId] = @UserId
          AND  [Code]   = @Code
          AND  YEAR([GeneratedAtUtc])  = @Year
          AND  MONTH([GeneratedAtUtc]) = @Month
          AND  (@CategoryId IS NULL OR [RelatedCategoryId] = @CategoryId)
    ) THEN 1 ELSE 0 END;
END
GO


-- =============================================================================
-- C4 — Recovery Mode: SIMPLE → FULL
-- =============================================================================
--
-- CRITICAL OPERATIONAL SEQUENCE — read before executing:
--
--   STEP 1  Switch to FULL recovery.
--   STEP 2  Take a full backup IMMEDIATELY (within seconds of STEP 1).
--           Without this, the transaction log enters an "unsupported" growth
--           mode and will fill the disk.
--   STEP 3  Configure a recurring log-backup job (every 15 minutes recommended).
--           Until STEP 3 is running the log cannot be truncated.
--
--   Replace [BACKUP_PATH] with an actual network or local backup directory.
--   Verify the backup path exists and SQL Server has write access before running.
--
-- =============================================================================

-- STEP 1: Switch to FULL recovery
ALTER DATABASE [MyMoney] SET RECOVERY FULL;
GO

-- STEP 2: Immediate full backup (run THIS within seconds of STEP 1)
-- BACKUP DATABASE [MyMoney]
-- TO   DISK = N'[BACKUP_PATH]\MyMoney_Full_RecoverySwitch.bak'
-- WITH FORMAT, INIT, COMPRESSION,
--      NAME  = N'MyMoney Full – Post Recovery-Mode Switch',
--      STATS = 10;
-- GO

-- STEP 3: Transaction-log backup template — wire this into a SQL Agent job
--         scheduled every 15 minutes.
-- BACKUP LOG [MyMoney]
-- TO   DISK = N'[BACKUP_PATH]\MyMoney_Log.bak'
-- WITH INIT, COMPRESSION, STATS = 10;
-- GO

-- NOTE: Monitor [MyMoney].ldf growth after switching.
--       If log file grows unexpectedly, verify the log-backup job is running:
--         SELECT log_reuse_wait_desc FROM sys.databases WHERE name = 'MyMoney';
--       A value of 'LOG_BACKUP' means the log-backup job has not yet run.


-- =============================================================================
-- C5 — Fix: usp_FIL_Transaction_GetLargeRecent
-- =============================================================================
-- Change from INNER JOIN to LEFT JOIN so users with no 3-month expense history
-- (new users or users with a gap) are still evaluated against @AbsoluteMin.
--
-- ISNULL(ua.[UserAverage], 0) ensures LargeTransactionDbResult.UserAverage
-- maps to decimal (non-nullable) in C# without Dapper throwing on NULL.
--
-- The two-branch WHERE condition is intentional:
--   Branch A — established users: Amount > 2 × their average expense
--   Branch B — new users (no baseline): Amount ≥ @AbsoluteMin threshold
-- =============================================================================

ALTER PROCEDURE [MyMoney].[usp_FIL_Transaction_GetLargeRecent]
    @FromUtc     DATETIME2(0),
    @AbsoluteMin DECIMAL(18,3) = 1000
AS
BEGIN
    SET NOCOUNT ON;

    WITH UserAverages AS (
        SELECT
            t.[UserId],
            AVG(t.[Amount]) AS UserAverage
        FROM  [MyMoney].[Transactions]     t
        JOIN  [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
        WHERE tt.[Name]       = 'Expense'
          AND t.[CreatedAt] >= DATEADD(MONTH, -3, GETUTCDATE())
          AND t.[CreatedAt] <  @FromUtc
        GROUP BY t.[UserId]
    )
    SELECT
        t.[UserId],
        t.[TransactionId]           AS TransactionId,
        t.[Amount],
        ISNULL(ua.[UserAverage], 0) AS UserAverage,
        c.[NameEn]                  AS CategoryNameEn,
        c.[NameAr]                  AS CategoryNameAr
    FROM  [MyMoney].[Transactions]     t
    JOIN  [MyMoney].[TransactionTypes] tt ON tt.[Id]        = t.[TransactionTypeId]
    JOIN  [MyMoney].[Categories]        c ON  c.[CategoryId] = t.[CategoryId]
    LEFT JOIN UserAverages             ua ON ua.[UserId]     = t.[UserId]
    WHERE tt.[Name]       = 'Expense'
      AND t.[CreatedAt] >= @FromUtc
      AND (
               (ua.[UserAverage] IS NOT NULL AND ua.[UserAverage] > 0 AND t.[Amount] > ua.[UserAverage] * 2)
            OR (ua.[UserAverage] IS NULL     AND t.[Amount] >= @AbsoluteMin)
          );
END
GO
