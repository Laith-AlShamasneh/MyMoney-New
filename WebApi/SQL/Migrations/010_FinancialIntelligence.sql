-- =============================================================================
-- Migration: 010_FinancialIntelligence
-- Description: Financial Intelligence Layer (FIL).
--              - CREATE TABLE: UserFinancialSnapshots, CategorySpendingAnalytics,
--                              FinancialInsights, SpendingPatterns,
--                              FinancialRecommendations
--              - Seed: 6 FIL notification templates (EN/AR)
--              - CREATE: 21 stored procedures
-- Author: Laith Al-Shamasneh
-- Date: 2026-06-17
-- =============================================================================


-- =============================================================================
-- 1. UserFinancialSnapshots
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserFinancialSnapshots' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[UserFinancialSnapshots]
    (
        [SnapshotId]              BIGINT        NOT NULL IDENTITY(1, 1),
        [UserId]                  BIGINT        NOT NULL,
        [SnapshotDate]            DATE          NOT NULL,
        [PeriodType]              TINYINT       NOT NULL,   -- SnapshotPeriodType: 1=Daily, 2=Monthly
        [TotalIncome]             DECIMAL(18,2) NOT NULL CONSTRAINT [DF_UFS_TotalIncome]             DEFAULT 0,
        [TotalExpense]            DECIMAL(18,2) NOT NULL CONSTRAINT [DF_UFS_TotalExpense]            DEFAULT 0,
        [NetBalance]              DECIMAL(18,2) NOT NULL CONSTRAINT [DF_UFS_NetBalance]              DEFAULT 0,
        [AverageDailySpend]       DECIMAL(18,2) NOT NULL CONSTRAINT [DF_UFS_AverageDailySpend]       DEFAULT 0,
        [AverageTransactionValue] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_UFS_AverageTransactionValue] DEFAULT 0,
        [TransactionCount]        INT           NOT NULL CONSTRAINT [DF_UFS_TransactionCount]        DEFAULT 0,
        [TopCategoryId]           INT           NULL,
        [CreatedAtUtc]            DATETIME2(0)  NOT NULL CONSTRAINT [DF_UFS_CreatedAtUtc]            DEFAULT GETUTCDATE(),
        [UpdatedAtUtc]            DATETIME2(0)  NOT NULL CONSTRAINT [DF_UFS_UpdatedAtUtc]            DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_UserFinancialSnapshots]             PRIMARY KEY CLUSTERED ([SnapshotId]),
        CONSTRAINT [UQ_UFS_UserId_SnapshotDate_PeriodType] UNIQUE ([UserId], [SnapshotDate], [PeriodType]),
        CONSTRAINT [FK_UFS_Users]      FOREIGN KEY ([UserId])        REFERENCES [MyMoney].[Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UFS_Categories] FOREIGN KEY ([TopCategoryId]) REFERENCES [MyMoney].[Categories]([Id]),
        CONSTRAINT [CK_UFS_PeriodType] CHECK ([PeriodType] BETWEEN 1 AND 2)
    );

    CREATE NONCLUSTERED INDEX [IX_UFS_UserId_SnapshotDate]
        ON [MyMoney].[UserFinancialSnapshots] ([UserId], [SnapshotDate] DESC)
        INCLUDE ([PeriodType], [TotalIncome], [TotalExpense], [NetBalance]);
END
GO

-- =============================================================================
-- 2. CategorySpendingAnalytics
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CategorySpendingAnalytics' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[CategorySpendingAnalytics]
    (
        [Id]                  BIGINT        NOT NULL IDENTITY(1, 1),
        [UserId]              BIGINT        NOT NULL,
        [CategoryId]          INT           NOT NULL,
        [PeriodStart]         DATE          NOT NULL,
        [PeriodEnd]           DATE          NOT NULL,
        [TotalSpent]          DECIMAL(18,2) NOT NULL CONSTRAINT [DF_CSA_TotalSpent]          DEFAULT 0,
        [TransactionCount]    INT           NOT NULL CONSTRAINT [DF_CSA_TransactionCount]    DEFAULT 0,
        [AverageSpent]        DECIMAL(18,2) NOT NULL CONSTRAINT [DF_CSA_AverageSpent]        DEFAULT 0,
        [PercentageOfTotal]   DECIMAL(8,2)  NOT NULL CONSTRAINT [DF_CSA_PercentageOfTotal]   DEFAULT 0,
        [TrendDirection]      TINYINT       NOT NULL CONSTRAINT [DF_CSA_TrendDirection]      DEFAULT 1,  -- 1=Stable
        [PreviousPeriodTotal] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_CSA_PreviousPeriodTotal] DEFAULT 0,
        [ChangePercentage]    DECIMAL(8,2)  NOT NULL CONSTRAINT [DF_CSA_ChangePercentage]    DEFAULT 0,
        [UpdatedAtUtc]        DATETIME2(0)  NOT NULL CONSTRAINT [DF_CSA_UpdatedAtUtc]        DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_CategorySpendingAnalytics]              PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_CSA_UserId_CategoryId_PeriodStart]      UNIQUE ([UserId], [CategoryId], [PeriodStart]),
        CONSTRAINT [FK_CSA_Users]      FOREIGN KEY ([UserId])      REFERENCES [MyMoney].[Users]([Id])       ON DELETE CASCADE,
        CONSTRAINT [FK_CSA_Categories] FOREIGN KEY ([CategoryId])  REFERENCES [MyMoney].[Categories]([Id]),
        CONSTRAINT [CK_CSA_TrendDirection] CHECK ([TrendDirection] BETWEEN 1 AND 3)
    );

    CREATE NONCLUSTERED INDEX [IX_CSA_UserId_PeriodStart]
        ON [MyMoney].[CategorySpendingAnalytics] ([UserId], [PeriodStart] DESC)
        INCLUDE ([CategoryId], [TotalSpent], [TrendDirection]);
END
GO

-- =============================================================================
-- 3. FinancialInsights
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FinancialInsights' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[FinancialInsights]
    (
        [InsightId]         BIGINT         NOT NULL IDENTITY(1, 1),
        [UserId]            BIGINT         NOT NULL,
        [Type]              TINYINT        NOT NULL,   -- InsightType: 1=Warning, 2=Info, 3=Opportunity, 4=Achievement
        [Code]              NVARCHAR(50)   NOT NULL,
        [TitleEn]           NVARCHAR(200)  NOT NULL,
        [TitleAr]           NVARCHAR(200)  NOT NULL,
        [DescriptionEn]     NVARCHAR(1000) NOT NULL,
        [DescriptionAr]     NVARCHAR(1000) NOT NULL,
        [Severity]          TINYINT        NOT NULL,   -- InsightSeverity: 1=Low, 2=Medium, 3=High, 4=Critical
        [RelatedCategoryId] INT            NULL,
        [DataPointJson]     NVARCHAR(MAX)  NULL,
        [IsRead]            BIT            NOT NULL CONSTRAINT [DF_FI_IsRead]            DEFAULT 0,
        [GeneratedAtUtc]    DATETIME2(0)   NOT NULL CONSTRAINT [DF_FI_GeneratedAtUtc]    DEFAULT GETUTCDATE(),
        [ReadAtUtc]         DATETIME2(0)   NULL,
        [ExpiresAtUtc]      DATETIME2(0)   NULL,

        CONSTRAINT [PK_FinancialInsights]   PRIMARY KEY CLUSTERED ([InsightId]),
        CONSTRAINT [FK_FI_Users]      FOREIGN KEY ([UserId])            REFERENCES [MyMoney].[Users]([Id])       ON DELETE CASCADE,
        CONSTRAINT [FK_FI_Categories] FOREIGN KEY ([RelatedCategoryId]) REFERENCES [MyMoney].[Categories]([Id]),
        CONSTRAINT [CK_FI_Type]     CHECK ([Type]     BETWEEN 1 AND 4),
        CONSTRAINT [CK_FI_Severity] CHECK ([Severity] BETWEEN 1 AND 4)
    );

    CREATE NONCLUSTERED INDEX [IX_FI_UserId_IsRead_Severity]
        ON [MyMoney].[FinancialInsights] ([UserId], [IsRead], [Severity] DESC)
        INCLUDE ([Type], [Code], [GeneratedAtUtc], [ExpiresAtUtc]);

    CREATE NONCLUSTERED INDEX [IX_FI_ExpiresAtUtc]
        ON [MyMoney].[FinancialInsights] ([ExpiresAtUtc])
        WHERE [ExpiresAtUtc] IS NOT NULL;

    CREATE NONCLUSTERED INDEX [IX_FI_UserId_Code_GeneratedAt]
        ON [MyMoney].[FinancialInsights] ([UserId], [Code], [GeneratedAtUtc] DESC);
END
GO

-- =============================================================================
-- 4. SpendingPatterns
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SpendingPatterns' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[SpendingPatterns]
    (
        [PatternId]       BIGINT        NOT NULL IDENTITY(1, 1),
        [UserId]          BIGINT        NOT NULL,
        [PatternType]     TINYINT       NOT NULL,   -- PatternType: 1=Daily, 2=Weekly, 3=Monthly, 4=CategoryBased
        [Code]            NVARCHAR(50)  NOT NULL,
        [DescriptionEn]   NVARCHAR(500) NOT NULL,
        [DescriptionAr]   NVARCHAR(500) NOT NULL,
        [ConfidenceScore] DECIMAL(5,2)  NOT NULL CONSTRAINT [DF_SP_ConfidenceScore] DEFAULT 0,
        [DataPointJson]   NVARCHAR(MAX) NULL,
        [DetectedAtUtc]   DATETIME2(0)  NOT NULL CONSTRAINT [DF_SP_DetectedAtUtc]   DEFAULT GETUTCDATE(),
        [ValidUntilUtc]   DATETIME2(0)  NULL,

        CONSTRAINT [PK_SpendingPatterns]        PRIMARY KEY CLUSTERED ([PatternId]),
        CONSTRAINT [UQ_SP_UserId_Code]          UNIQUE ([UserId], [Code]),
        CONSTRAINT [FK_SP_Users] FOREIGN KEY ([UserId]) REFERENCES [MyMoney].[Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_SP_PatternType]     CHECK ([PatternType]     BETWEEN 1 AND 4),
        CONSTRAINT [CK_SP_ConfidenceScore] CHECK ([ConfidenceScore] BETWEEN 0 AND 100)
    );

    CREATE NONCLUSTERED INDEX [IX_SP_UserId_ValidUntil]
        ON [MyMoney].[SpendingPatterns] ([UserId], [ValidUntilUtc])
        WHERE [ValidUntilUtc] IS NOT NULL;
END
GO

-- =============================================================================
-- 5. FinancialRecommendations
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FinancialRecommendations' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[FinancialRecommendations]
    (
        [RecommendationId]   BIGINT        NOT NULL IDENTITY(1, 1),
        [UserId]             BIGINT        NOT NULL,
        [Type]               TINYINT       NOT NULL,   -- RecommendationType: 1-5
        [Code]               NVARCHAR(50)  NOT NULL,
        [TitleEn]            NVARCHAR(200) NOT NULL,
        [TitleAr]            NVARCHAR(200) NOT NULL,
        [MessageEn]          NVARCHAR(1000) NOT NULL,
        [MessageAr]          NVARCHAR(1000) NOT NULL,
        [ExpectedImpactValue] DECIMAL(18,2) NULL,
        [Priority]           TINYINT       NOT NULL CONSTRAINT [DF_FR_Priority]    DEFAULT 2,
        [RelatedCategoryId]  INT           NULL,
        [IsApplied]          BIT           NOT NULL CONSTRAINT [DF_FR_IsApplied]   DEFAULT 0,
        [IsDismissed]        BIT           NOT NULL CONSTRAINT [DF_FR_IsDismissed] DEFAULT 0,
        [CreatedAtUtc]       DATETIME2(0)  NOT NULL CONSTRAINT [DF_FR_CreatedAtUtc] DEFAULT GETUTCDATE(),
        [ActionedAtUtc]      DATETIME2(0)  NULL,
        [ExpiresAtUtc]       DATETIME2(0)  NULL,

        CONSTRAINT [PK_FinancialRecommendations]  PRIMARY KEY CLUSTERED ([RecommendationId]),
        CONSTRAINT [FK_FR_Users]      FOREIGN KEY ([UserId])           REFERENCES [MyMoney].[Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_FR_Categories] FOREIGN KEY ([RelatedCategoryId]) REFERENCES [MyMoney].[Categories]([Id]),
        CONSTRAINT [CK_FR_Type]     CHECK ([Type]     BETWEEN 1 AND 5),
        CONSTRAINT [CK_FR_Priority] CHECK ([Priority] BETWEEN 1 AND 3)
    );

    CREATE NONCLUSTERED INDEX [IX_FR_UserId_Active]
        ON [MyMoney].[FinancialRecommendations] ([UserId], [IsApplied], [IsDismissed], [Priority])
        INCLUDE ([Type], [Code], [CreatedAtUtc], [ExpiresAtUtc]);

    CREATE NONCLUSTERED INDEX [IX_FR_UserId_Code_CreatedAt]
        ON [MyMoney].[FinancialRecommendations] ([UserId], [Code], [CreatedAtUtc] DESC);
END
GO


-- =============================================================================
-- 6. FIL Notification template seeds
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = 'FIL_OverspendingAlert')
BEGIN
    DECLARE @FILOverspendId    INT, @FILSpikeId     INT, @FILUnusualId   INT,
            @FILHighExpId      INT, @FILAchieveId   INT, @FILSummaryId   INT;

    -- Financial Intelligence (Category=2)
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('FIL_OverspendingAlert', 2, 3, 2); SET @FILOverspendId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('FIL_SpendingSpike', 2, 3, 3); SET @FILSpikeId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('FIL_UnusualTransaction', 2, 3, 2); SET @FILUnusualId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('FIL_HighExpenseRatio', 2, 4, 2); SET @FILHighExpId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('FIL_Achievement', 2, 2, 4); SET @FILAchieveId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('FIL_MonthlySummary', 2, 1, 4); SET @FILSummaryId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplateTranslations]
        ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
    VALUES
    -- FIL_OverspendingAlert
    (@FILOverspendId, 'en', 'Overspending Alert: {CategoryName}',
                            'Your spending on {CategoryName} has reached {UsagePercent}% of your monthly average.'),
    (@FILOverspendId, 'ar', N'تنبيه الإنفاق الزائد: {CategoryName}',
                            N'وصل إنفاقك على {CategoryName} إلى {UsagePercent}% من متوسطك الشهري.'),

    -- FIL_SpendingSpike
    (@FILSpikeId,     'en', 'Spending Spike: {CategoryName}',
                            'Your spending on {CategoryName} increased by {ChangePercent}% compared to last month.'),
    (@FILSpikeId,     'ar', N'ارتفاع في الإنفاق: {CategoryName}',
                            N'ارتفع إنفاقك على {CategoryName} بنسبة {ChangePercent}% مقارنةً بالشهر الماضي.'),

    -- FIL_UnusualTransaction
    (@FILUnusualId,   'en', 'Unusual Transaction Detected',
                            'A transaction of {Amount} was recorded — {Multiple}× your average transaction value.'),
    (@FILUnusualId,   'ar', N'تم اكتشاف معاملة غير عادية',
                            N'تم تسجيل معاملة بقيمة {Amount} — وهي {Multiple}× متوسط معاملاتك.'),

    -- FIL_HighExpenseRatio
    (@FILHighExpId,   'en', 'High Expense Ratio',
                            'Your expenses this month are high relative to your income. Review your spending to stay on track.'),
    (@FILHighExpId,   'ar', N'نسبة مصروفات مرتفعة',
                            N'مصروفاتك هذا الشهر مرتفعة نسبةً إلى دخلك. راجع إنفاقك للبقاء على المسار الصحيح.'),

    -- FIL_Achievement
    (@FILAchieveId,   'en', 'Financial Achievement Unlocked',
                            'Great work! You''ve reached a new financial milestone. Keep it up!'),
    (@FILAchieveId,   'ar', N'إنجاز مالي جديد!',
                            N'أحسنت! لقد حققت إنجازاً مالياً جديداً. استمر في هذا المسار!'),

    -- FIL_MonthlySummary
    (@FILSummaryId,   'en', 'Your Monthly Financial Summary',
                            'Your financial summary for {Month} is ready. Review your insights and recommendations.'),
    (@FILSummaryId,   'ar', N'ملخصك المالي الشهري',
                            N'ملخصك المالي لشهر {Month} جاهز. راجع رؤاك وتوصياتك المالية.');
END
GO


-- =============================================================================
-- STORED PROCEDURES
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Snapshot SPs
-- ─────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID('MyMoney.usp_FIL_Snapshot_Compute', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Snapshot_Compute];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Snapshot_Compute]
    @UserId BIGINT,
    @Year   INT,
    @Month  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DaysInMonth INT = DAY(EOMONTH(DATEFROMPARTS(@Year, @Month, 1)));

    SELECT
        ISNULL(SUM(CASE WHEN tt.[Name] = 'Income'  THEN t.[Amount] ELSE 0 END), 0)
            AS TotalIncome,
        ISNULL(SUM(CASE WHEN tt.[Name] = 'Expense' THEN t.[Amount] ELSE 0 END), 0)
            AS TotalExpense,
        ISNULL(SUM(CASE WHEN tt.[Name] = 'Income'  THEN t.[Amount] ELSE -t.[Amount] END), 0)
            AS NetBalance,
        ISNULL(
            SUM(CASE WHEN tt.[Name] = 'Expense' THEN t.[Amount] ELSE 0 END) /
            NULLIF(@DaysInMonth, 0), 0)
            AS AverageDailySpend,
        ISNULL(AVG(t.[Amount]), 0)
            AS AverageTransactionValue,
        COUNT(t.[Id])
            AS TransactionCount,
        (
            SELECT TOP 1 t2.[CategoryId]
            FROM   [MyMoney].[Transactions] t2
            JOIN   [MyMoney].[TransactionTypes] tt2 ON tt2.[Id] = t2.[TransactionTypeId]
            WHERE  t2.[UserId] = @UserId
              AND  YEAR(t2.[TransactionDate])  = @Year
              AND  MONTH(t2.[TransactionDate]) = @Month
              AND  tt2.[Name] = 'Expense'
            GROUP  BY t2.[CategoryId]
            ORDER  BY SUM(t2.[Amount]) DESC
        ) AS TopCategoryId
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE t.[UserId] = @UserId
      AND YEAR(t.[TransactionDate])  = @Year
      AND MONTH(t.[TransactionDate]) = @Month;
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_Snapshot_Upsert', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Snapshot_Upsert];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Snapshot_Upsert]
    @UserId                  BIGINT,
    @SnapshotDate            DATE,
    @PeriodType              TINYINT,
    @TotalIncome             DECIMAL(18,2),
    @TotalExpense            DECIMAL(18,2),
    @NetBalance              DECIMAL(18,2),
    @AverageDailySpend       DECIMAL(18,2),
    @AverageTransactionValue DECIMAL(18,2),
    @TransactionCount        INT,
    @TopCategoryId           INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    MERGE [MyMoney].[UserFinancialSnapshots] AS target
    USING (SELECT @UserId, @SnapshotDate, @PeriodType)
        AS source(UserId, SnapshotDate, PeriodType)
    ON target.[UserId]       = source.UserId
   AND target.[SnapshotDate] = source.SnapshotDate
   AND target.[PeriodType]   = source.PeriodType
    WHEN MATCHED THEN
        UPDATE SET
            [TotalIncome]             = @TotalIncome,
            [TotalExpense]            = @TotalExpense,
            [NetBalance]              = @NetBalance,
            [AverageDailySpend]       = @AverageDailySpend,
            [AverageTransactionValue] = @AverageTransactionValue,
            [TransactionCount]        = @TransactionCount,
            [TopCategoryId]           = @TopCategoryId,
            [UpdatedAtUtc]            = GETUTCDATE()
    WHEN NOT MATCHED THEN
        INSERT ([UserId], [SnapshotDate], [PeriodType], [TotalIncome], [TotalExpense],
                [NetBalance], [AverageDailySpend], [AverageTransactionValue],
                [TransactionCount], [TopCategoryId])
        VALUES (@UserId, @SnapshotDate, @PeriodType, @TotalIncome, @TotalExpense,
                @NetBalance, @AverageDailySpend, @AverageTransactionValue,
                @TransactionCount, @TopCategoryId);
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_Snapshot_GetLatest', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Snapshot_GetLatest];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Snapshot_GetLatest]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        [SnapshotId], [SnapshotDate], [PeriodType],
        [TotalIncome], [TotalExpense], [NetBalance],
        [AverageDailySpend], [AverageTransactionValue],
        [TransactionCount], [TopCategoryId], [UpdatedAtUtc]
    FROM  [MyMoney].[UserFinancialSnapshots]
    WHERE [UserId]     = @UserId
      AND [PeriodType] = 2   -- Monthly
    ORDER BY [SnapshotDate] DESC;
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_Snapshot_GetRecent', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Snapshot_GetRecent];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Snapshot_GetRecent]
    @UserId BIGINT,
    @Months INT = 3
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Months)
        [SnapshotId], [SnapshotDate], [PeriodType],
        [TotalIncome], [TotalExpense], [NetBalance],
        [AverageDailySpend], [AverageTransactionValue],
        [TransactionCount], [TopCategoryId], [UpdatedAtUtc]
    FROM  [MyMoney].[UserFinancialSnapshots]
    WHERE [UserId]     = @UserId
      AND [PeriodType] = 2   -- Monthly
    ORDER BY [SnapshotDate] DESC;
END
GO


-- ─────────────────────────────────────────────────────────────────────────────
-- Category Analytics SPs
-- ─────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID('MyMoney.usp_FIL_CategoryAnalytics_Compute', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_CategoryAnalytics_Compute];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_CategoryAnalytics_Compute]
    @UserId BIGINT,
    @Year   INT,
    @Month  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PrevYear  INT = CASE WHEN @Month = 1 THEN @Year - 1 ELSE @Year  END;
    DECLARE @PrevMonth INT = CASE WHEN @Month = 1 THEN 12        ELSE @Month - 1 END;

    DECLARE @TotalExpenses DECIMAL(18,2);
    SELECT  @TotalExpenses = SUM(t.[Amount])
    FROM    [MyMoney].[Transactions] t
    JOIN    [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    WHERE   t.[UserId] = @UserId
      AND   YEAR(t.[TransactionDate])  = @Year
      AND   MONTH(t.[TransactionDate]) = @Month
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
        DATEFROMPARTS(@Year, @Month, 1)                               AS PeriodStart,
        EOMONTH(DATEFROMPARTS(@Year, @Month, 1))                      AS PeriodEnd
    FROM [MyMoney].[Transactions] t
    JOIN [MyMoney].[Categories] c        ON c.[Id]  = t.[CategoryId]
    JOIN [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    LEFT JOIN (
        SELECT tp.[CategoryId], SUM(tp.[Amount]) AS PrevTotal
        FROM   [MyMoney].[Transactions] tp
        JOIN   [MyMoney].[TransactionTypes] ttp ON ttp.[Id] = tp.[TransactionTypeId]
        WHERE  tp.[UserId] = @UserId
          AND  YEAR(tp.[TransactionDate])  = @PrevYear
          AND  MONTH(tp.[TransactionDate]) = @PrevMonth
          AND  ttp.[Name] = 'Expense'
        GROUP  BY tp.[CategoryId]
    ) prev ON prev.[CategoryId] = t.[CategoryId]
    WHERE t.[UserId] = @UserId
      AND YEAR(t.[TransactionDate])  = @Year
      AND MONTH(t.[TransactionDate]) = @Month
      AND tt.[Name] = 'Expense'
    GROUP BY c.[Id], c.[NameEn], c.[NameAr], prev.[PrevTotal];
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_CategoryAnalytics_Upsert', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_CategoryAnalytics_Upsert];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_CategoryAnalytics_Upsert]
    @UserId              BIGINT,
    @CategoryId          INT,
    @PeriodStart         DATE,
    @PeriodEnd           DATE,
    @TotalSpent          DECIMAL(18,2),
    @TransactionCount    INT,
    @AverageSpent        DECIMAL(18,2),
    @PercentageOfTotal   DECIMAL(8,2),
    @TrendDirection      TINYINT,
    @PreviousPeriodTotal DECIMAL(18,2),
    @ChangePercentage    DECIMAL(8,2)
AS
BEGIN
    SET NOCOUNT ON;

    MERGE [MyMoney].[CategorySpendingAnalytics] AS target
    USING (SELECT @UserId, @CategoryId, @PeriodStart)
        AS source(UserId, CategoryId, PeriodStart)
    ON target.[UserId]      = source.UserId
   AND target.[CategoryId]  = source.CategoryId
   AND target.[PeriodStart] = source.PeriodStart
    WHEN MATCHED THEN
        UPDATE SET
            [PeriodEnd]           = @PeriodEnd,
            [TotalSpent]          = @TotalSpent,
            [TransactionCount]    = @TransactionCount,
            [AverageSpent]        = @AverageSpent,
            [PercentageOfTotal]   = @PercentageOfTotal,
            [TrendDirection]      = @TrendDirection,
            [PreviousPeriodTotal] = @PreviousPeriodTotal,
            [ChangePercentage]    = @ChangePercentage,
            [UpdatedAtUtc]        = GETUTCDATE()
    WHEN NOT MATCHED THEN
        INSERT ([UserId], [CategoryId], [PeriodStart], [PeriodEnd], [TotalSpent],
                [TransactionCount], [AverageSpent], [PercentageOfTotal],
                [TrendDirection], [PreviousPeriodTotal], [ChangePercentage])
        VALUES (@UserId, @CategoryId, @PeriodStart, @PeriodEnd, @TotalSpent,
                @TransactionCount, @AverageSpent, @PercentageOfTotal,
                @TrendDirection, @PreviousPeriodTotal, @ChangePercentage);
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_CategoryAnalytics_GetByPeriod', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_CategoryAnalytics_GetByPeriod];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_CategoryAnalytics_GetByPeriod]
    @UserId BIGINT,
    @Year   INT,
    @Month  INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PeriodStart DATE = DATEFROMPARTS(@Year, @Month, 1);

    SELECT
        csa.[Id],
        csa.[CategoryId],
        c.[NameEn]                AS CategoryNameEn,
        c.[NameAr]                AS CategoryNameAr,
        csa.[TotalSpent],
        csa.[TransactionCount],
        csa.[AverageSpent],
        csa.[PercentageOfTotal],
        csa.[TrendDirection],
        csa.[PreviousPeriodTotal],
        csa.[ChangePercentage],
        csa.[PeriodStart],
        csa.[PeriodEnd]
    FROM  [MyMoney].[CategorySpendingAnalytics] csa
    JOIN  [MyMoney].[Categories] c ON c.[Id] = csa.[CategoryId]
    WHERE csa.[UserId]      = @UserId
      AND csa.[PeriodStart] = @PeriodStart
    ORDER BY csa.[TotalSpent] DESC;
END
GO


-- ─────────────────────────────────────────────────────────────────────────────
-- Insight SPs
-- ─────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID('MyMoney.usp_FIL_Insight_Create', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Insight_Create];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Insight_Create]
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

    INSERT INTO [MyMoney].[FinancialInsights]
        ([UserId], [Type], [Code], [TitleEn], [TitleAr],
         [DescriptionEn], [DescriptionAr], [Severity],
         [RelatedCategoryId], [DataPointJson], [ExpiresAtUtc])
    VALUES
        (@UserId, @Type, @Code, @TitleEn, @TitleAr,
         @DescriptionEn, @DescriptionAr, @Severity,
         @RelatedCategoryId, @DataPointJson, @ExpiresAtUtc);

    SET @NewId = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_Insight_GetList', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Insight_GetList];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Insight_GetList]
    @UserId     BIGINT,
    @IsRead     BIT  = NULL,
    @PageNumber INT  = 1,
    @PageSize   INT  = 20
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- Result set 1: paginated insights
    SELECT
        [InsightId], [Type], [Code], [TitleEn], [TitleAr],
        [DescriptionEn], [DescriptionAr], [Severity],
        [RelatedCategoryId], [DataPointJson], [IsRead],
        [GeneratedAtUtc], [ExpiresAtUtc]
    FROM  [MyMoney].[FinancialInsights]
    WHERE [UserId] = @UserId
      AND (@IsRead IS NULL OR [IsRead] = @IsRead)
      AND ([ExpiresAtUtc] IS NULL OR [ExpiresAtUtc] > GETUTCDATE())
    ORDER BY [Severity] DESC, [GeneratedAtUtc] DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    -- Result set 2: counts
    SELECT
        COUNT(*)                                                 AS TotalCount,
        SUM(CASE WHEN [IsRead] = 0 THEN 1 ELSE 0 END)          AS UnreadCount
    FROM  [MyMoney].[FinancialInsights]
    WHERE [UserId] = @UserId
      AND (@IsRead IS NULL OR [IsRead] = @IsRead)
      AND ([ExpiresAtUtc] IS NULL OR [ExpiresAtUtc] > GETUTCDATE());
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_Insight_MarkRead', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Insight_MarkRead];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Insight_MarkRead]
    @UserId       BIGINT,
    @InsightId    BIGINT,
    @RowsAffected INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[FinancialInsights]
    SET    [IsRead]    = 1,
           [ReadAtUtc] = GETUTCDATE()
    WHERE  [InsightId] = @InsightId
      AND  [UserId]    = @UserId
      AND  [IsRead]    = 0;

    SET @RowsAffected = @@ROWCOUNT;
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_Insight_ExistsForMonth', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Insight_ExistsForMonth];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Insight_ExistsForMonth]
    @UserId BIGINT,
    @Code   NVARCHAR(50),
    @Year   INT,
    @Month  INT
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
    ) THEN 1 ELSE 0 END;
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_Insight_CleanupExpired', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Insight_CleanupExpired];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Insight_CleanupExpired]
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [MyMoney].[FinancialInsights]
    WHERE  [ExpiresAtUtc] IS NOT NULL
      AND  [ExpiresAtUtc] < GETUTCDATE();
END
GO


-- ─────────────────────────────────────────────────────────────────────────────
-- Pattern SPs
-- ─────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID('MyMoney.usp_FIL_Pattern_Upsert', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Pattern_Upsert];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Pattern_Upsert]
    @UserId          BIGINT,
    @PatternType     TINYINT,
    @Code            NVARCHAR(50),
    @DescriptionEn   NVARCHAR(500),
    @DescriptionAr   NVARCHAR(500),
    @ConfidenceScore DECIMAL(5,2),
    @DataPointJson   NVARCHAR(MAX) = NULL,
    @ValidUntilUtc   DATETIME2(0)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    MERGE [MyMoney].[SpendingPatterns] AS target
    USING (SELECT @UserId, @Code) AS source(UserId, Code)
    ON target.[UserId] = source.UserId
   AND target.[Code]   = source.Code
    WHEN MATCHED THEN
        UPDATE SET
            [PatternType]     = @PatternType,
            [DescriptionEn]   = @DescriptionEn,
            [DescriptionAr]   = @DescriptionAr,
            [ConfidenceScore] = @ConfidenceScore,
            [DataPointJson]   = @DataPointJson,
            [ValidUntilUtc]   = @ValidUntilUtc,
            [DetectedAtUtc]   = GETUTCDATE()
    WHEN NOT MATCHED THEN
        INSERT ([UserId], [PatternType], [Code], [DescriptionEn], [DescriptionAr],
                [ConfidenceScore], [DataPointJson], [ValidUntilUtc])
        VALUES (@UserId, @PatternType, @Code, @DescriptionEn, @DescriptionAr,
                @ConfidenceScore, @DataPointJson, @ValidUntilUtc);
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_Pattern_GetByUser', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Pattern_GetByUser];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Pattern_GetByUser]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [PatternId], [PatternType], [Code],
        [DescriptionEn], [DescriptionAr], [ConfidenceScore], [DetectedAtUtc]
    FROM  [MyMoney].[SpendingPatterns]
    WHERE [UserId] = @UserId
      AND ([ValidUntilUtc] IS NULL OR [ValidUntilUtc] > GETUTCDATE())
    ORDER BY [ConfidenceScore] DESC;
END
GO


-- ─────────────────────────────────────────────────────────────────────────────
-- Recommendation SPs
-- ─────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID('MyMoney.usp_FIL_Recommendation_Create', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Recommendation_Create];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Recommendation_Create]
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

    INSERT INTO [MyMoney].[FinancialRecommendations]
        ([UserId], [Type], [Code], [TitleEn], [TitleAr],
         [MessageEn], [MessageAr], [ExpectedImpactValue], [Priority],
         [RelatedCategoryId], [ExpiresAtUtc])
    VALUES
        (@UserId, @Type, @Code, @TitleEn, @TitleAr,
         @MessageEn, @MessageAr, @ExpectedImpactValue, @Priority,
         @RelatedCategoryId, @ExpiresAtUtc);

    SET @NewId = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_Recommendation_GetList', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Recommendation_GetList];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Recommendation_GetList]
    @UserId     BIGINT,
    @PageNumber INT = 1,
    @PageSize   INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- Result set 1: active paginated recommendations
    SELECT
        [RecommendationId], [Type], [Code], [TitleEn], [TitleAr],
        [MessageEn], [MessageAr], [ExpectedImpactValue], [Priority],
        [RelatedCategoryId], [IsApplied], [IsDismissed], [CreatedAtUtc]
    FROM  [MyMoney].[FinancialRecommendations]
    WHERE [UserId]      = @UserId
      AND [IsApplied]   = 0
      AND [IsDismissed] = 0
      AND ([ExpiresAtUtc] IS NULL OR [ExpiresAtUtc] > GETUTCDATE())
    ORDER BY [Priority] ASC, [CreatedAtUtc] DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

    -- Result set 2: total active count
    SELECT COUNT(*) AS TotalCount
    FROM   [MyMoney].[FinancialRecommendations]
    WHERE  [UserId]      = @UserId
      AND  [IsApplied]   = 0
      AND  [IsDismissed] = 0
      AND  ([ExpiresAtUtc] IS NULL OR [ExpiresAtUtc] > GETUTCDATE());
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_Recommendation_MarkApplied', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Recommendation_MarkApplied];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Recommendation_MarkApplied]
    @UserId           BIGINT,
    @RecommendationId BIGINT,
    @RowsAffected     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[FinancialRecommendations]
    SET    [IsApplied]    = 1,
           [ActionedAtUtc] = GETUTCDATE()
    WHERE  [RecommendationId] = @RecommendationId
      AND  [UserId]           = @UserId
      AND  [IsApplied]        = 0;

    SET @RowsAffected = @@ROWCOUNT;
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_Recommendation_Dismiss', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Recommendation_Dismiss];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Recommendation_Dismiss]
    @UserId           BIGINT,
    @RecommendationId BIGINT,
    @RowsAffected     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[FinancialRecommendations]
    SET    [IsDismissed]   = 1,
           [ActionedAtUtc] = GETUTCDATE()
    WHERE  [RecommendationId] = @RecommendationId
      AND  [UserId]           = @UserId
      AND  [IsDismissed]      = 0;

    SET @RowsAffected = @@ROWCOUNT;
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_Recommendation_ExistsForMonth', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Recommendation_ExistsForMonth];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Recommendation_ExistsForMonth]
    @UserId BIGINT,
    @Code   NVARCHAR(50),
    @Year   INT,
    @Month  INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CASE WHEN EXISTS (
        SELECT 1
        FROM   [MyMoney].[FinancialRecommendations]
        WHERE  [UserId] = @UserId
          AND  [Code]   = @Code
          AND  YEAR([CreatedAtUtc])  = @Year
          AND  MONTH([CreatedAtUtc]) = @Month
    ) THEN 1 ELSE 0 END;
END
GO


-- ─────────────────────────────────────────────────────────────────────────────
-- Anomaly detection + active users SPs
-- ─────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID('MyMoney.usp_FIL_Transaction_GetLargeRecent', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_Transaction_GetLargeRecent];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_Transaction_GetLargeRecent]
    @FromUtc DATETIME2(0)
AS
BEGIN
    SET NOCOUNT ON;

    -- Return expense transactions since @FromUtc that exceed 2× the user's
    -- 3-month rolling average expense transaction value.
    WITH UserAverages AS (
        SELECT
            t.[UserId],
            AVG(t.[Amount]) AS UserAverage
        FROM  [MyMoney].[Transactions] t
        JOIN  [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
        WHERE tt.[Name] = 'Expense'
          AND t.[TransactionDate] >= DATEADD(MONTH, -3, GETUTCDATE())
        GROUP BY t.[UserId]
    )
    SELECT
        t.[UserId],
        t.[Id]     AS TransactionId,
        t.[Amount],
        ua.[UserAverage],
        c.[NameEn] AS CategoryNameEn,
        c.[NameAr] AS CategoryNameAr
    FROM  [MyMoney].[Transactions] t
    JOIN  [MyMoney].[TransactionTypes] tt ON tt.[Id] = t.[TransactionTypeId]
    JOIN  [MyMoney].[Categories]        c  ON c.[Id]  = t.[CategoryId]
    JOIN  UserAverages ua                  ON ua.[UserId] = t.[UserId]
    WHERE tt.[Name] = 'Expense'
      AND CAST(t.[TransactionDate] AS DATETIME2) >= @FromUtc
      AND ua.[UserAverage] > 0
      AND t.[Amount] > ua.[UserAverage] * 2;
END
GO

IF OBJECT_ID('MyMoney.usp_FIL_User_GetActive', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_FIL_User_GetActive];
GO
CREATE PROCEDURE [MyMoney].[usp_FIL_User_GetActive]
    @ActiveDays INT = 30
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT t.[UserId]
    FROM   [MyMoney].[Transactions] t
    WHERE  t.[TransactionDate] >= DATEADD(DAY, -@ActiveDays, GETUTCDATE());
END
GO
