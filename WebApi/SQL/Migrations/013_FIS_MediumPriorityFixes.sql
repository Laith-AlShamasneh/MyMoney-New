-- =============================================================================
-- Migration: 013_FIS_MediumPriorityFixes
-- Description: Resolves eight Medium-severity findings from the FIS audit.
--
--   M1 — NetBalance formula: treat only Expense rows as negative
--        ALTER: usp_FIL_Snapshot_Compute
--
--   M2 — Recommendation cleanup strategy (unbounded table growth)
--        CREATE: usp_FIL_Recommendation_Cleanup
--
--   M4 — N+1 CategoryAnalytics upsert  → single bulk call via OPENJSON
--        CREATE: usp_FIL_CategoryAnalytics_BulkUpsert
--
--   M5 — Notification category-name localization (single {CategoryName})
--        UPDATE: NotificationTemplateTranslations for OverspendingAlert,
--                SpendingSpike, and the new PositiveBehavior / ConsistentSaver
--
--   M6 — Snapshot index missing PeriodType as key column
--        DROP / CREATE: IX_UFS_UserId_PeriodType_SnapshotDate
--
--   M8 — PositiveBehavior and ConsistentSaver share FIL_Achievement code
--        INSERT: FIL_PositiveBehavior, FIL_ConsistentSaver templates
--
-- Author: Laith Al-Shamasneh
-- Date:   2026-06-17
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
GO
SET ANSI_NULLS ON;
GO


-- =============================================================================
-- M1 — Fix: usp_FIL_Snapshot_Compute — NetBalance formula
-- =============================================================================
-- Bug: ELSE -t.[Amount] negates Transfer and other non-Expense types.
-- Fix: explicit CASE for Expense only; unrecognised types contribute 0.
-- The remainder of the procedure is identical to migration 012.
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

    -- For the current in-progress month use elapsed days; for completed months
    -- use the full month length (AverageDailySpend denominator fix from H5).
    DECLARE @ElapsedDays INT = CASE
        WHEN @Year  = YEAR(@Today)
         AND @Month = MONTH(@Today) THEN ISNULL(NULLIF(DAY(@Today), 0), 1)
        ELSE @DaysInMonth
    END;

    SELECT
        ISNULL(SUM(CASE WHEN tt.[Name] = 'Income'  THEN t.[Amount] ELSE 0 END), 0)   AS TotalIncome,
        ISNULL(SUM(CASE WHEN tt.[Name] = 'Expense' THEN t.[Amount] ELSE 0 END), 0)   AS TotalExpense,
        -- M1 fix: only Income adds, only Expense subtracts; other types contribute 0.
        ISNULL(SUM(CASE
            WHEN tt.[Name] = 'Income'  THEN  t.[Amount]
            WHEN tt.[Name] = 'Expense' THEN -t.[Amount]
            ELSE 0
        END), 0)                                                                      AS NetBalance,
        ISNULL(
            SUM(CASE WHEN tt.[Name] = 'Expense' THEN t.[Amount] ELSE 0 END)
            / NULLIF(@ElapsedDays, 0),
        0)                                                                            AS AverageDailySpend,
        ISNULL(AVG(t.[Amount]), 0)                                                    AS AverageTransactionValue,
        COUNT(*)                                                                      AS TransactionCount,
        (
            SELECT TOP 1 t2.[CategoryId]
            FROM   [MyMoney].[Transactions]     t2
            JOIN   [MyMoney].[TransactionTypes] tt2 ON tt2.[Id] = t2.[TransactionTypeId]
            WHERE  t2.[UserId]              = @UserId
              AND  tt2.[Name]              = 'Expense'
              AND  t2.[TransactionDate]   >= @PeriodStart
              AND  t2.[TransactionDate]    < @PeriodEnd
            GROUP  BY t2.[CategoryId]
            ORDER  BY SUM(t2.[Amount]) DESC
        )                                                                             AS TopCategoryId
    FROM  [MyMoney].[Transactions]     t
    JOIN  [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId]            = @UserId
      AND t.[TransactionDate]  >= @PeriodStart
      AND t.[TransactionDate]   < @PeriodEnd;
END
GO


-- =============================================================================
-- M2 — Create: usp_FIL_Recommendation_Cleanup
-- =============================================================================
-- Retention policy (30-day grace period in both branches):
--   • Acted rows (applied OR dismissed): deleted 30 days after action.
--   • Expired-without-action rows: deleted 30 days after the expiry date.
-- Active, non-expired, un-actioned recommendations are never touched.
-- =============================================================================

IF OBJECT_ID('MyMoney.usp_FIL_Recommendation_Cleanup', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Recommendation_Cleanup];
GO

CREATE PROCEDURE [MyMoney].[usp_FIL_Recommendation_Cleanup]
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [MyMoney].[FinancialRecommendations]
    WHERE
        -- Applied or dismissed: keep 30 days after the action date for short-term history.
        (
            ([IsApplied] = 1 OR [IsDismissed] = 1)
            AND [ActionedAtUtc] IS NOT NULL
            AND [ActionedAtUtc] < DATEADD(DAY, -30, GETUTCDATE())
        )
        OR
        -- Expired without user action: keep 30 days after expiry for delayed review.
        (
            [IsApplied]    = 0
            AND [IsDismissed] = 0
            AND [ExpiresAtUtc] IS NOT NULL
            AND [ExpiresAtUtc] < DATEADD(DAY, -30, GETUTCDATE())
        );
END
GO


-- =============================================================================
-- M4 — Create: usp_FIL_CategoryAnalytics_BulkUpsert
-- =============================================================================
-- Replaces the per-row usp_FIL_CategoryAnalytics_Upsert N+1 pattern.
-- Accepts a JSON array; uses OPENJSON + MERGE for a single round-trip.
-- =============================================================================

IF OBJECT_ID('MyMoney.usp_FIL_CategoryAnalytics_BulkUpsert', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_CategoryAnalytics_BulkUpsert];
GO

CREATE PROCEDURE [MyMoney].[usp_FIL_CategoryAnalytics_BulkUpsert]
    @UserId           BIGINT,
    @CategoryDataJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    MERGE [MyMoney].[CategorySpendingAnalytics] AS target
    USING (
        SELECT
            @UserId                                AS UserId,
            j.[CategoryId],
            CAST(j.[PeriodStart] AS DATE)          AS PeriodStart,
            CAST(j.[PeriodEnd]   AS DATE)          AS PeriodEnd,
            j.[TotalSpent],
            j.[TransactionCount],
            j.[AverageSpent],
            j.[PercentageOfTotal],
            j.[TrendDirection],
            j.[PreviousPeriodTotal],
            j.[ChangePercentage]
        FROM OPENJSON(@CategoryDataJson)
        WITH (
            [CategoryId]          INT           '$.categoryId',
            [PeriodStart]         NVARCHAR(10)  '$.periodStart',
            [PeriodEnd]           NVARCHAR(10)  '$.periodEnd',
            [TotalSpent]          DECIMAL(18,2) '$.totalSpent',
            [TransactionCount]    INT           '$.transactionCount',
            [AverageSpent]        DECIMAL(18,2) '$.averageSpent',
            [PercentageOfTotal]   DECIMAL(8,2)  '$.percentageOfTotal',
            [TrendDirection]      TINYINT       '$.trendDirection',
            [PreviousPeriodTotal] DECIMAL(18,2) '$.previousPeriodTotal',
            [ChangePercentage]    DECIMAL(8,2)  '$.changePercentage'
        ) j
    ) AS source
    ON  target.[UserId]      = source.UserId
    AND target.[CategoryId]  = source.[CategoryId]
    AND target.[PeriodStart] = source.[PeriodStart]
    WHEN MATCHED THEN
        UPDATE SET
            [PeriodEnd]           = source.[PeriodEnd],
            [TotalSpent]          = source.[TotalSpent],
            [TransactionCount]    = source.[TransactionCount],
            [AverageSpent]        = source.[AverageSpent],
            [PercentageOfTotal]   = source.[PercentageOfTotal],
            [TrendDirection]      = source.[TrendDirection],
            [PreviousPeriodTotal] = source.[PreviousPeriodTotal],
            [ChangePercentage]    = source.[ChangePercentage],
            [UpdatedAtUtc]        = GETUTCDATE()
    WHEN NOT MATCHED THEN
        INSERT (
            [UserId], [CategoryId], [PeriodStart], [PeriodEnd],
            [TotalSpent], [TransactionCount], [AverageSpent], [PercentageOfTotal],
            [TrendDirection], [PreviousPeriodTotal], [ChangePercentage]
        )
        VALUES (
            source.UserId, source.[CategoryId], source.[PeriodStart], source.[PeriodEnd],
            source.[TotalSpent], source.[TransactionCount], source.[AverageSpent], source.[PercentageOfTotal],
            source.[TrendDirection], source.[PreviousPeriodTotal], source.[ChangePercentage]
        );
END
GO


-- =============================================================================
-- M5 — Update: notification templates to use bilingual category-name params
-- =============================================================================
-- Replace the single {CategoryName} placeholder with language-specific variants:
--   English templates → {CategoryNameEn}
--   Arabic templates  → {CategoryNameAr}
-- This enables the notification publisher to substitute the correct language
-- without embedding English names in Arabic push notifications.
-- =============================================================================

-- FIL_OverspendingAlert
UPDATE [MyMoney].[NotificationTemplateTranslations]
SET    [TitleTemplate]   = 'Overspending Alert: {CategoryNameEn}',
       [MessageTemplate] = 'Your spending on {CategoryNameEn} has reached {UsagePercent}% of your monthly average.'
WHERE  [TemplateId] = (SELECT [Id] FROM [MyMoney].[NotificationTemplates] WHERE [Code] = 'FIL_OverspendingAlert')
  AND  [LanguageCode] = 'en';

UPDATE [MyMoney].[NotificationTemplateTranslations]
SET    [TitleTemplate]   = N'تنبيه الإنفاق الزائد: {CategoryNameAr}',
       [MessageTemplate] = N'وصل إنفاقك على {CategoryNameAr} إلى {UsagePercent}% من متوسطك الشهري.'
WHERE  [TemplateId] = (SELECT [Id] FROM [MyMoney].[NotificationTemplates] WHERE [Code] = 'FIL_OverspendingAlert')
  AND  [LanguageCode] = 'ar';

-- FIL_SpendingSpike
UPDATE [MyMoney].[NotificationTemplateTranslations]
SET    [TitleTemplate]   = 'Spending Spike: {CategoryNameEn}',
       [MessageTemplate] = 'Your spending on {CategoryNameEn} increased by {ChangePercent}% compared to last month.'
WHERE  [TemplateId] = (SELECT [Id] FROM [MyMoney].[NotificationTemplates] WHERE [Code] = 'FIL_SpendingSpike')
  AND  [LanguageCode] = 'en';

UPDATE [MyMoney].[NotificationTemplateTranslations]
SET    [TitleTemplate]   = N'ارتفاع في الإنفاق: {CategoryNameAr}',
       [MessageTemplate] = N'ارتفع إنفاقك على {CategoryNameAr} بنسبة {ChangePercent}% مقارنةً بالشهر الماضي.'
WHERE  [TemplateId] = (SELECT [Id] FROM [MyMoney].[NotificationTemplates] WHERE [Code] = 'FIL_SpendingSpike')
  AND  [LanguageCode] = 'ar';
GO


-- =============================================================================
-- M6 — Replace snapshot index: add PeriodType as a key column
-- =============================================================================
-- Before: IX_UFS_UserId_SnapshotDate  (UserId, SnapshotDate DESC)
--         PeriodType was only in INCLUDE — a scan was required to filter it.
-- After:  IX_UFS_UserId_PeriodType_SnapshotDate (UserId, PeriodType, SnapshotDate DESC)
--         Both usp_FIL_Snapshot_GetLatest and usp_FIL_Snapshot_GetRecent filter
--         WHERE UserId = x AND PeriodType = 2 ORDER BY SnapshotDate DESC,
--         so this index now fully satisfies both queries via index seek + range scan.
-- =============================================================================

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  object_id = OBJECT_ID('MyMoney.UserFinancialSnapshots')
      AND  name      = 'IX_UFS_UserId_SnapshotDate'
)
    DROP INDEX [IX_UFS_UserId_SnapshotDate] ON [MyMoney].[UserFinancialSnapshots];
GO

CREATE NONCLUSTERED INDEX [IX_UFS_UserId_PeriodType_SnapshotDate]
    ON [MyMoney].[UserFinancialSnapshots] ([UserId] ASC, [PeriodType] ASC, [SnapshotDate] DESC)
    INCLUDE (
        [TotalIncome],
        [TotalExpense],
        [NetBalance],
        [AverageDailySpend],
        [AverageTransactionValue],
        [TransactionCount],
        [TopCategoryId]
    );
GO


-- =============================================================================
-- M8 — Add: FIL_PositiveBehavior and FIL_ConsistentSaver notification templates
-- =============================================================================
-- PositiveBehavior and ConsistentSaver previously shared FIL_Achievement,
-- preventing per-type routing, filtering, or analytics.
-- FIL_Achievement is kept for backward compatibility with any existing rows.
-- =============================================================================

DECLARE @FILPosBehavId   INT;
DECLARE @FILConsistentId INT;

IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = 'FIL_PositiveBehavior')
BEGIN
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('FIL_PositiveBehavior', 2, 2, 4);
    SET @FILPosBehavId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplateTranslations]
        ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
    VALUES
        (@FILPosBehavId, 'en',
         'Great Job: {CategoryNameEn}',
         'You reduced spending on {CategoryNameEn} by {ReductionPercent}% compared to last month. Keep it up!'),
        (@FILPosBehavId, 'ar',
         N'أحسنت: {CategoryNameAr}',
         N'لقد قللت إنفاقك على {CategoryNameAr} بنسبة {ReductionPercent}% مقارنةً بالشهر الماضي. استمر في ذلك!');
END;

IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = 'FIL_ConsistentSaver')
BEGIN
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('FIL_ConsistentSaver', 2, 2, 4);
    SET @FILConsistentId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplateTranslations]
        ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
    VALUES
        (@FILConsistentId, 'en',
         'Consistent Saver Achievement',
         'You''ve maintained a positive net balance for {MonthCount} consecutive months. Excellent financial discipline!'),
        (@FILConsistentId, 'ar',
         N'إنجاز المدخر الثابت',
         N'حافظت على رصيد صافي إيجابي لمدة {MonthCount} أشهر متتالية. انضباط مالي ممتاز!');
END;
GO
