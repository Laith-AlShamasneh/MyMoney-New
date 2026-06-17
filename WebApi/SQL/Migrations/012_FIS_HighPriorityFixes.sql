-- =============================================================================
-- Migration: 012_FIS_HighPriorityFixes
-- Description: Resolves all six High-severity findings from the FIS audit.
--
--   H1 — Non-sargable YEAR()/MONTH() predicates on TransactionDate
--        ALTER: usp_FIL_Snapshot_Compute
--               usp_FIL_CategoryAnalytics_Compute
--               usp_FIL_User_GetActive
--
--   H2 — Non-sargable YEAR()/MONTH() predicates on datetime audit columns
--        Index: IX_FI_UserId_Code_GeneratedAt (add RelatedCategoryId INCLUDE)
--        ALTER: usp_FIL_Insight_ExistsForMonth
--               usp_FIL_Recommendation_ExistsForMonth
--
--   H3 — Unsafe session defaults (ANSI_NULLS OFF, ANSI_WARNINGS OFF,
--         QUOTED_IDENTIFIER OFF inherited from database creation)
--        SET QUOTED_IDENTIFIER ON / SET ANSI_NULLS ON at migration level
--        ALTER DATABASE: ANSI_NULLS ON, ANSI_WARNINGS ON
--
--   H4 — READ_COMMITTED_SNAPSHOT disabled; readers block writers
--        ALTER DATABASE: READ_COMMITTED_SNAPSHOT ON
--
--   H5 — AverageDailySpend divides by total days even for in-progress months
--        ALTER: usp_FIL_Snapshot_Compute (combined with H1 fix for same SP)
--
--   H6 — Race condition in insight/recommendation creation
--        ALTER: usp_FIL_Insight_Create
--               usp_FIL_Recommendation_Create
--        (C# guard in FinancialIntelligenceService also updated.)
--
-- Author: Laith Al-Shamasneh
-- Date:   2026-06-17
-- =============================================================================


-- H3: All CREATE/ALTER PROCEDURE statements in this session will be compiled
-- with QUOTED_IDENTIFIER ON and ANSI_NULLS ON — baked into sys.sql_modules.
SET QUOTED_IDENTIFIER ON;
GO
SET ANSI_NULLS ON;
GO


-- =============================================================================
-- H3 — Correct database session defaults
-- =============================================================================
-- These become the defaults for every new connection that does not override them
-- at the session level. ADO.NET/SqlClient already sends SET ANSI_NULLS ON etc.
-- at connection open, so application behaviour is unchanged; this fixes tooling
-- (SSMS, SQLCMD) and any future stored-procedure deployments that rely on the
-- database defaults.
-- =============================================================================

ALTER DATABASE [MyMoney] SET ANSI_NULLS    ON;
ALTER DATABASE [MyMoney] SET ANSI_WARNINGS ON;
GO


-- =============================================================================
-- H4 — Enable Read-Committed Snapshot Isolation
-- =============================================================================
-- With RCSI ON, READ COMMITTED readers take no shared locks and instead read
-- from row-version snapshots. This eliminates reader/writer blocking on the
-- FIS workload: background job writes no longer block dashboard reads.
--
-- OPERATIONAL NOTE:
--   ALTER DATABASE SET READ_COMMITTED_SNAPSHOT ON requires exclusive database
--   access internally while building the version store bootstrap. SQL Server
--   waits for active READ COMMITTED transactions to drain before completing —
--   it does NOT force-disconnect sessions. Run during a low-traffic window,
--   or use WITH ROLLBACK IMMEDIATE if immediate completion is needed:
--
--     ALTER DATABASE [MyMoney]
--         SET READ_COMMITTED_SNAPSHOT ON
--         WITH ROLLBACK IMMEDIATE;
--
--   Monitor progress:
--     SELECT name, is_read_committed_snapshot_on
--     FROM   sys.databases
--     WHERE  name = 'MyMoney';
-- =============================================================================

ALTER DATABASE [MyMoney] SET READ_COMMITTED_SNAPSHOT ON;
GO


-- =============================================================================
-- H2 — Improve covering index on FinancialInsights
-- =============================================================================
-- Add RelatedCategoryId as INCLUDE so the ExistsForMonth and atomic-create
-- queries are covering: the optimizer can satisfy the predicate
-- (@CategoryId IS NULL OR [RelatedCategoryId] = @CategoryId) from the index
-- without a key lookup into the clustered index.
-- =============================================================================

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name      = 'IX_FI_UserId_Code_GeneratedAt'
      AND  object_id = OBJECT_ID('MyMoney.FinancialInsights')
)
    DROP INDEX [IX_FI_UserId_Code_GeneratedAt]
        ON [MyMoney].[FinancialInsights];
GO

CREATE NONCLUSTERED INDEX [IX_FI_UserId_Code_GeneratedAt]
    ON [MyMoney].[FinancialInsights] ([UserId], [Code], [GeneratedAtUtc] DESC)
    INCLUDE ([RelatedCategoryId]);
GO


-- =============================================================================
-- H1 + H5 — usp_FIL_Snapshot_Compute
-- =============================================================================
-- H1: Replace YEAR(TransactionDate) = @Year AND MONTH(TransactionDate) = @Month
--     with a sargable half-open DATE range so the existing index on
--     (UserId, TransactionDate) can be used for a seek instead of a scan.
--
-- H5: For the current calendar month use elapsed days (1..today) as the
--     divisor rather than total days in the month. For past months the
--     behaviour is unchanged (elapsed = total days).
-- =============================================================================

ALTER PROCEDURE [MyMoney].[usp_FIL_Snapshot_Compute]
    @UserId BIGINT,
    @Year   INT,
    @Month  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today       DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @PeriodStart DATE = DATEFROMPARTS(@Year, @Month, 1);
    DECLARE @PeriodEnd   DATE = DATEADD(MONTH, 1, @PeriodStart);
    DECLARE @DaysInMonth INT  = DAY(EOMONTH(@PeriodStart));

    -- H5: active month → divide by days elapsed so far; closed month → full month.
    DECLARE @ElapsedDays INT = CASE
        WHEN @Year  = YEAR(@Today)
         AND @Month = MONTH(@Today) THEN ISNULL(NULLIF(DAY(@Today), 0), 1)
        ELSE @DaysInMonth
    END;

    SELECT
        ISNULL(SUM(CASE WHEN tt.[Name] = 'Income'  THEN t.[Amount] ELSE 0 END), 0)
            AS TotalIncome,
        ISNULL(SUM(CASE WHEN tt.[Name] = 'Expense' THEN t.[Amount] ELSE 0 END), 0)
            AS TotalExpense,
        ISNULL(SUM(CASE WHEN tt.[Name] = 'Income'  THEN t.[Amount] ELSE -t.[Amount] END), 0)
            AS NetBalance,
        ISNULL(
            SUM(CASE WHEN tt.[Name] = 'Expense' THEN t.[Amount] ELSE 0 END) /
            NULLIF(@ElapsedDays, 0), 0)
            AS AverageDailySpend,
        ISNULL(AVG(t.[Amount]), 0)
            AS AverageTransactionValue,
        COUNT(t.[Id])
            AS TransactionCount,
        (
            SELECT TOP 1 t2.[CategoryId]
            FROM   [MyMoney].[Transactions] t2
            JOIN   [MyMoney].[TransactionTypes] tt2 ON tt2.[Id] = t2.[TransactionTypeId]
            WHERE  t2.[UserId]          = @UserId
              AND  t2.[TransactionDate] >= @PeriodStart
              AND  t2.[TransactionDate] <  @PeriodEnd
              AND  tt2.[Name] = 'Expense'
            GROUP  BY t2.[CategoryId]
            ORDER  BY SUM(t2.[Amount]) DESC
        ) AS TopCategoryId
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId]          = @UserId
      AND t.[TransactionDate] >= @PeriodStart
      AND t.[TransactionDate] <  @PeriodEnd;
END
GO


-- =============================================================================
-- H1 — usp_FIL_CategoryAnalytics_Compute
-- =============================================================================
-- Replace three separate YEAR/MONTH predicate blocks (TotalExpenses CTE,
-- previous-period sub-query, and main query) with sargable DATE range variables.
-- =============================================================================

ALTER PROCEDURE [MyMoney].[usp_FIL_CategoryAnalytics_Compute]
    @UserId BIGINT,
    @Year   INT,
    @Month  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PrevYear  INT = CASE WHEN @Month = 1 THEN @Year - 1 ELSE @Year  END;
    DECLARE @PrevMonth INT = CASE WHEN @Month = 1 THEN 12        ELSE @Month - 1 END;

    DECLARE @PeriodStart     DATE = DATEFROMPARTS(@Year,     @Month,     1);
    DECLARE @PeriodEnd       DATE = DATEADD(MONTH, 1, @PeriodStart);
    DECLARE @PrevPeriodStart DATE = DATEFROMPARTS(@PrevYear, @PrevMonth, 1);
    DECLARE @PrevPeriodEnd   DATE = DATEADD(MONTH, 1, @PrevPeriodStart);

    DECLARE @TotalExpenses DECIMAL(18,2);
    SELECT  @TotalExpenses = SUM(t.[Amount])
    FROM    [MyMoney].[Transactions] t
    JOIN    [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE   t.[UserId]          = @UserId
      AND   t.[TransactionDate] >= @PeriodStart
      AND   t.[TransactionDate] <  @PeriodEnd
      AND   tt.[Name] = 'Expense';

    SELECT
        0                                                             AS Id,
        c.[Id]                                                        AS CategoryId,
        c.[NameEn]                                                    AS CategoryNameEn,
        c.[NameAr]                                                    AS CategoryNameAr,
        SUM(t.[Amount])                                               AS TotalSpent,
        COUNT(t.[Id])                                                 AS TransactionCount,
        AVG(t.[Amount])                                               AS AverageSpent,
        ROUND(SUM(t.[Amount]) / NULLIF(@TotalExpenses, 0) * 100, 2)  AS PercentageOfTotal,
        CAST(CASE
            WHEN ISNULL(prev.[PrevTotal], 0) = 0 THEN 1
            WHEN SUM(t.[Amount]) > prev.[PrevTotal] * 1.05 THEN 2
            WHEN SUM(t.[Amount]) < prev.[PrevTotal] * 0.95 THEN 3
            ELSE 1
        END AS TINYINT)                                               AS TrendDirection,
        ISNULL(prev.[PrevTotal], 0)                                   AS PreviousPeriodTotal,
        CASE
            WHEN ISNULL(prev.[PrevTotal], 0) = 0 THEN 0.00
            ELSE ROUND((SUM(t.[Amount]) - prev.[PrevTotal]) / prev.[PrevTotal] * 100, 2)
        END                                                           AS ChangePercentage,
        @PeriodStart                                                  AS PeriodStart,
        EOMONTH(@PeriodStart)                                         AS PeriodEnd
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[Categories] c        ON c.[Id]  = t.[CategoryId]
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    LEFT JOIN (
        SELECT tp.[CategoryId], SUM(tp.[Amount]) AS PrevTotal
        FROM   [MyMoney].[Transactions] tp
        JOIN   [MyMoney].[TransactionTypes] ttp ON ttp.[Id] = tp.[TransactionTypeId]
        WHERE  tp.[UserId]          = @UserId
          AND  tp.[TransactionDate] >= @PrevPeriodStart
          AND  tp.[TransactionDate] <  @PrevPeriodEnd
          AND  ttp.[Name] = 'Expense'
        GROUP  BY tp.[CategoryId]
    ) prev ON prev.[CategoryId] = t.[CategoryId]
    WHERE t.[UserId]          = @UserId
      AND t.[TransactionDate] >= @PeriodStart
      AND t.[TransactionDate] <  @PeriodEnd
      AND tt.[Name] = 'Expense'
    GROUP BY c.[Id], c.[NameEn], c.[NameAr], prev.[PrevTotal];
END
GO


-- =============================================================================
-- H1 (minor) — usp_FIL_User_GetActive
-- =============================================================================
-- The original WHERE clause compared a DATE column to DATEADD(...GETUTCDATE()),
-- which returns DATETIME and includes a time component. SQL Server promotes the
-- DATE to DATETIME midnight for the comparison, causing transactions entered
-- before midnight on the boundary day to be incorrectly excluded in some
-- edge-case scenarios. Casting the threshold to DATE makes the comparison
-- explicit and semantically correct: "any transaction on or after this date".
-- =============================================================================

ALTER PROCEDURE [MyMoney].[usp_FIL_User_GetActive]
    @ActiveDays INT = 30
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Cutoff DATE = CAST(DATEADD(DAY, -@ActiveDays, GETUTCDATE()) AS DATE);

    SELECT DISTINCT t.[UserId]
    FROM   [MyMoney].[Transactions] t
    WHERE  t.[TransactionDate] >= @Cutoff;
END
GO


-- =============================================================================
-- H2 — usp_FIL_Insight_ExistsForMonth  (sargable date range)
-- =============================================================================
-- Replaces YEAR([GeneratedAtUtc]) = @Year AND MONTH([GeneratedAtUtc]) = @Month
-- with a half-open DATETIME2 range so the composite index
-- IX_FI_UserId_Code_GeneratedAt can be used for an index seek.
--
-- The @CategoryId parameter introduced by C3 (migration 011) is preserved.
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

    DECLARE @PeriodStart DATETIME2(0) = CAST(DATEFROMPARTS(@Year, @Month, 1) AS DATETIME2(0));
    DECLARE @PeriodEnd   DATETIME2(0) = DATEADD(MONTH, 1, @PeriodStart);

    SELECT CASE WHEN EXISTS (
        SELECT 1
        FROM   [MyMoney].[FinancialInsights]
        WHERE  [UserId]         = @UserId
          AND  [Code]           = @Code
          AND  [GeneratedAtUtc] >= @PeriodStart
          AND  [GeneratedAtUtc] <  @PeriodEnd
          AND  (@CategoryId IS NULL OR [RelatedCategoryId] = @CategoryId)
    ) THEN 1 ELSE 0 END;
END
GO


-- =============================================================================
-- H2 — usp_FIL_Recommendation_ExistsForMonth  (sargable date range)
-- =============================================================================
-- Same fix as above applied to CreatedAtUtc; allows IX_FR_UserId_Code_CreatedAt
-- to be used for an index seek rather than a full scan with function predicates.
-- =============================================================================

ALTER PROCEDURE [MyMoney].[usp_FIL_Recommendation_ExistsForMonth]
    @UserId BIGINT,
    @Code   NVARCHAR(50),
    @Year   INT,
    @Month  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PeriodStart DATETIME2(0) = CAST(DATEFROMPARTS(@Year, @Month, 1) AS DATETIME2(0));
    DECLARE @PeriodEnd   DATETIME2(0) = DATEADD(MONTH, 1, @PeriodStart);

    SELECT CASE WHEN EXISTS (
        SELECT 1
        FROM   [MyMoney].[FinancialRecommendations]
        WHERE  [UserId]       = @UserId
          AND  [Code]         = @Code
          AND  [CreatedAtUtc] >= @PeriodStart
          AND  [CreatedAtUtc] <  @PeriodEnd
    ) THEN 1 ELSE 0 END;
END
GO


-- =============================================================================
-- H6 — usp_FIL_Insight_Create  (atomic duplicate prevention)
-- =============================================================================
-- Converts the plain INSERT into a conditional INSERT ... SELECT ... WHERE NOT
-- EXISTS guarded by UPDLOCK + HOLDLOCK.
--
-- UPDLOCK converts the initial shared lock on the existence check to an update
-- lock; HOLDLOCK (= SERIALIZABLE) holds it until the statement finishes. Any
-- concurrent session performing the same check blocks until the first session
-- completes, preventing two threads from both reading "not exists" before either
-- inserts.
--
-- @NewId = 0 signals to the caller that the row already existed and no insert
-- was performed. The C# service uses this to suppress duplicate notifications.
--
-- The check scope matches usp_FIL_Insight_ExistsForMonth: same month as the
-- CURRENT wall-clock time, same Code + optional RelatedCategoryId.
-- =============================================================================

ALTER PROCEDURE [MyMoney].[usp_FIL_Insight_Create]
    @UserId            BIGINT,
    @Type              TINYINT,
    @Code              NVARCHAR(50),
    @TitleEn           NVARCHAR(200),
    @TitleAr           NVARCHAR(200),
    @DescriptionEn     NVARCHAR(1000),
    @DescriptionAr     NVARCHAR(1000),
    @Severity          TINYINT,
    @RelatedCategoryId INT           = NULL,
    @DataPointJson     NVARCHAR(MAX) = NULL,
    @ExpiresAtUtc      DATETIME2(0)  = NULL,
    @NewId             BIGINT        OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now         DATETIME2(0) = GETUTCDATE();
    DECLARE @PeriodStart DATETIME2(0) = CAST(DATEFROMPARTS(YEAR(@Now), MONTH(@Now), 1) AS DATETIME2(0));
    DECLARE @PeriodEnd   DATETIME2(0) = DATEADD(MONTH, 1, @PeriodStart);

    SET @NewId = 0;

    INSERT INTO [MyMoney].[FinancialInsights]
        ([UserId], [Type], [Code], [TitleEn], [TitleAr],
         [DescriptionEn], [DescriptionAr], [Severity],
         [RelatedCategoryId], [DataPointJson], [ExpiresAtUtc])
    SELECT
        @UserId, @Type, @Code, @TitleEn, @TitleAr,
        @DescriptionEn, @DescriptionAr, @Severity,
        @RelatedCategoryId, @DataPointJson, @ExpiresAtUtc
    WHERE NOT EXISTS (
        SELECT 1
        FROM   [MyMoney].[FinancialInsights] WITH (UPDLOCK, HOLDLOCK)
        WHERE  [UserId]         = @UserId
          AND  [Code]           = @Code
          AND  [GeneratedAtUtc] >= @PeriodStart
          AND  [GeneratedAtUtc] <  @PeriodEnd
          AND  (@RelatedCategoryId IS NULL OR [RelatedCategoryId] = @RelatedCategoryId)
    );

    IF @@ROWCOUNT > 0
        SET @NewId = SCOPE_IDENTITY();
END
GO


-- =============================================================================
-- H6 — usp_FIL_Recommendation_Create  (atomic duplicate prevention)
-- =============================================================================
-- Same pattern as usp_FIL_Insight_Create above, applied to recommendations.
-- Code-scoped (no RelatedCategoryId in the uniqueness check) to match
-- usp_FIL_Recommendation_ExistsForMonth semantics.
-- =============================================================================

ALTER PROCEDURE [MyMoney].[usp_FIL_Recommendation_Create]
    @UserId              BIGINT,
    @Type                TINYINT,
    @Code                NVARCHAR(50),
    @TitleEn             NVARCHAR(200),
    @TitleAr             NVARCHAR(200),
    @MessageEn           NVARCHAR(1000),
    @MessageAr           NVARCHAR(1000),
    @ExpectedImpactValue DECIMAL(18,2) = NULL,
    @Priority            TINYINT       = 2,
    @RelatedCategoryId   INT           = NULL,
    @ExpiresAtUtc        DATETIME2(0)  = NULL,
    @NewId               BIGINT        OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now         DATETIME2(0) = GETUTCDATE();
    DECLARE @PeriodStart DATETIME2(0) = CAST(DATEFROMPARTS(YEAR(@Now), MONTH(@Now), 1) AS DATETIME2(0));
    DECLARE @PeriodEnd   DATETIME2(0) = DATEADD(MONTH, 1, @PeriodStart);

    SET @NewId = 0;

    INSERT INTO [MyMoney].[FinancialRecommendations]
        ([UserId], [Type], [Code], [TitleEn], [TitleAr],
         [MessageEn], [MessageAr], [ExpectedImpactValue], [Priority],
         [RelatedCategoryId], [ExpiresAtUtc])
    SELECT
        @UserId, @Type, @Code, @TitleEn, @TitleAr,
        @MessageEn, @MessageAr, @ExpectedImpactValue, @Priority,
        @RelatedCategoryId, @ExpiresAtUtc
    WHERE NOT EXISTS (
        SELECT 1
        FROM   [MyMoney].[FinancialRecommendations] WITH (UPDLOCK, HOLDLOCK)
        WHERE  [UserId]       = @UserId
          AND  [Code]         = @Code
          AND  [CreatedAtUtc] >= @PeriodStart
          AND  [CreatedAtUtc] <  @PeriodEnd
    );

    IF @@ROWCOUNT > 0
        SET @NewId = SCOPE_IDENTITY();
END
GO
