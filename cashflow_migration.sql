-- =============================================================================
-- Cash Flow Forecasting System — Migration Script
-- Description : Creates all tables, indexes, stored procedures, and
--               notification template rows required by the CashFlow module.
-- Run order   : Execute the entire script in a single SSMS batch.
-- Safe to re-run : CREATE OR ALTER used for all SPs.
--                  Tables and indexes use IF NOT EXISTS guards.
-- =============================================================================

USE [MyMoney];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- TABLES
-- ─────────────────────────────────────────────────────────────────────────────

-- 1. CashFlowForecasts — one row per user (latest forecast document)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[MyMoney].[CashFlowForecasts]'))
BEGIN
    CREATE TABLE [MyMoney].[CashFlowForecasts](
        [ForecastId]           BIGINT        IDENTITY(1,1) NOT NULL,
        [UserId]               BIGINT        NOT NULL,
        [GeneratedAtUtc]       DATETIME2(0)  NOT NULL,
        [HorizonMonths]        TINYINT       NOT NULL CONSTRAINT [DF_CFF_HorizonMonths]        DEFAULT 12,
        [MonthsOfHistoryUsed]  TINYINT       NOT NULL CONSTRAINT [DF_CFF_MonthsOfHistoryUsed]  DEFAULT 0,
        [CurrentBalanceEst]    DECIMAL(18,2) NOT NULL CONSTRAINT [DF_CFF_CurrentBalanceEst]    DEFAULT 0,
        [OverallConfidence]    DECIMAL(5,2)  NOT NULL CONSTRAINT [DF_CFF_OverallConfidence]    DEFAULT 0,
        [ConfidenceBand]       TINYINT       NOT NULL CONSTRAINT [DF_CFF_ConfidenceBand]       DEFAULT 1,
        [RecurringIncomeMthly] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_CFF_RecurringIncomeMthly] DEFAULT 0,
        [RecurringExpMthly]    DECIMAL(18,2) NOT NULL CONSTRAINT [DF_CFF_RecurringExpMthly]    DEFAULT 0,
        [AvgVarIncomeMthly]    DECIMAL(18,2) NOT NULL CONSTRAINT [DF_CFF_AvgVarIncomeMthly]    DEFAULT 0,
        [AvgVarExpMthly]       DECIMAL(18,2) NOT NULL CONSTRAINT [DF_CFF_AvgVarExpMthly]       DEFAULT 0,
        [ForecastedEndBalance] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_CFF_ForecastedEndBalance] DEFAULT 0,
        CONSTRAINT [PK_CashFlowForecasts]       PRIMARY KEY CLUSTERED ([ForecastId] ASC),
        CONSTRAINT [UQ_CashFlowForecasts_UserId] UNIQUE NONCLUSTERED ([UserId] ASC)
    ) ON [PRIMARY];
    PRINT 'Created table [MyMoney].[CashFlowForecasts]';
END
GO

-- 2. ForecastMonthlyPoints — up to 12 rows per forecast
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[MyMoney].[ForecastMonthlyPoints]'))
BEGIN
    CREATE TABLE [MyMoney].[ForecastMonthlyPoints](
        [PointId]          BIGINT        IDENTITY(1,1) NOT NULL,
        [ForecastId]       BIGINT        NOT NULL,
        [UserId]           BIGINT        NOT NULL,
        [MonthYear]        DATE          NOT NULL,
        [ProjectedIncome]  DECIMAL(18,2) NOT NULL,
        [ProjectedExpense] DECIMAL(18,2) NOT NULL,
        [ProjectedNet]     DECIMAL(18,2) NOT NULL,
        [RunningBalance]   DECIMAL(18,2) NOT NULL,
        [RecurringIncome]  DECIMAL(18,2) NOT NULL,
        [RecurringExpense] DECIMAL(18,2) NOT NULL,
        [VariableIncome]   DECIMAL(18,2) NOT NULL,
        [VariableExpense]  DECIMAL(18,2) NOT NULL,
        [ConfidenceScore]  DECIMAL(5,2)  NOT NULL,
        CONSTRAINT [PK_ForecastMonthlyPoints]         PRIMARY KEY CLUSTERED ([PointId] ASC),
        CONSTRAINT [UQ_FMP_ForecastId_MonthYear]      UNIQUE NONCLUSTERED ([ForecastId] ASC, [MonthYear] ASC),
        CONSTRAINT [FK_FMP_ForecastId]                FOREIGN KEY ([ForecastId])
            REFERENCES [MyMoney].[CashFlowForecasts]([ForecastId]) ON DELETE CASCADE
    ) ON [PRIMARY];
    PRINT 'Created table [MyMoney].[ForecastMonthlyPoints]';
END
GO

-- 3. ForecastRisks
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[MyMoney].[ForecastRisks]'))
BEGIN
    CREATE TABLE [MyMoney].[ForecastRisks](
        [RiskId]            BIGINT        IDENTITY(1,1) NOT NULL,
        [ForecastId]        BIGINT        NOT NULL,
        [UserId]            BIGINT        NOT NULL,
        [RiskType]          TINYINT       NOT NULL,
        [Severity]          TINYINT       NOT NULL,
        [TitleEn]           NVARCHAR(200) NOT NULL,
        [TitleAr]           NVARCHAR(200) NOT NULL,
        [DescriptionEn]     NVARCHAR(500) NOT NULL,
        [DescriptionAr]     NVARCHAR(500) NOT NULL,
        [AffectedMonthYear] DATE          NULL,
        [DataPointJson]     NVARCHAR(MAX) NULL,
        [NotifiedAtUtc]     DATETIME2(0)  NULL,
        CONSTRAINT [PK_ForecastRisks]  PRIMARY KEY CLUSTERED ([RiskId] ASC),
        CONSTRAINT [FK_FR_ForecastId]  FOREIGN KEY ([ForecastId])
            REFERENCES [MyMoney].[CashFlowForecasts]([ForecastId]) ON DELETE CASCADE
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
    PRINT 'Created table [MyMoney].[ForecastRisks]';
END
GO

-- 4. ForecastGoalProjections
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[MyMoney].[ForecastGoalProjections]'))
BEGIN
    CREATE TABLE [MyMoney].[ForecastGoalProjections](
        [ProjectionId]         BIGINT        IDENTITY(1,1) NOT NULL,
        [ForecastId]           BIGINT        NOT NULL,
        [UserId]               BIGINT        NOT NULL,
        [GoalId]               BIGINT        NOT NULL,
        [GoalName]             NVARCHAR(100) NOT NULL,
        [TargetAmount]         DECIMAL(18,2) NOT NULL,
        [CurrentAmount]        DECIMAL(18,2) NOT NULL,
        [TargetDate]           DATE          NULL,
        [RequiredMonthlyContr] DECIMAL(18,2) NOT NULL,
        [AvgMonthlyPace]       DECIMAL(18,2) NOT NULL,
        [EstimatedComplDate]   DATE          NULL,
        [IsAtRisk]             BIT           NOT NULL CONSTRAINT [DF_FGP_IsAtRisk] DEFAULT 0,
        [DaysToCompletion]     INT           NULL,
        CONSTRAINT [PK_ForecastGoalProjections]       PRIMARY KEY CLUSTERED ([ProjectionId] ASC),
        CONSTRAINT [UQ_FGP_ForecastId_GoalId]         UNIQUE NONCLUSTERED ([ForecastId] ASC, [GoalId] ASC),
        CONSTRAINT [FK_FGP_ForecastId]                FOREIGN KEY ([ForecastId])
            REFERENCES [MyMoney].[CashFlowForecasts]([ForecastId]) ON DELETE CASCADE
    ) ON [PRIMARY];
    PRINT 'Created table [MyMoney].[ForecastGoalProjections]';
END
GO

-- 5. ForecastScenarios — Phase 9 infrastructure foundation
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[MyMoney].[ForecastScenarios]'))
BEGIN
    CREATE TABLE [MyMoney].[ForecastScenarios](
        [ScenarioId]     BIGINT        IDENTITY(1,1) NOT NULL,
        [ForecastId]     BIGINT        NOT NULL,
        [UserId]         BIGINT        NOT NULL,
        [Name]           NVARCHAR(100) NOT NULL,
        [ParametersJson] NVARCHAR(MAX) NOT NULL,
        [CreatedAtUtc]   DATETIME2(0)  NOT NULL,
        CONSTRAINT [PK_ForecastScenarios] PRIMARY KEY CLUSTERED ([ScenarioId] ASC),
        CONSTRAINT [FK_FS_ForecastId]     FOREIGN KEY ([ForecastId])
            REFERENCES [MyMoney].[CashFlowForecasts]([ForecastId]) ON DELETE CASCADE
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
    PRINT 'Created table [MyMoney].[ForecastScenarios]';
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- INDEXES
-- ─────────────────────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ForecastMonthlyPoints_UserId' AND object_id = OBJECT_ID(N'[MyMoney].[ForecastMonthlyPoints]'))
    CREATE NONCLUSTERED INDEX [IX_ForecastMonthlyPoints_UserId]
    ON [MyMoney].[ForecastMonthlyPoints] ([UserId] ASC, [MonthYear] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ForecastRisks_UserId' AND object_id = OBJECT_ID(N'[MyMoney].[ForecastRisks]'))
    CREATE NONCLUSTERED INDEX [IX_ForecastRisks_UserId]
    ON [MyMoney].[ForecastRisks] ([UserId] ASC, [Severity] DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ForecastGoalProjections_UserId' AND object_id = OBJECT_ID(N'[MyMoney].[ForecastGoalProjections]'))
    CREATE NONCLUSTERED INDEX [IX_ForecastGoalProjections_UserId]
    ON [MyMoney].[ForecastGoalProjections] ([UserId] ASC);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- NOTIFICATION TEMPLATES
-- ─────────────────────────────────────────────────────────────────────────────

-- Insert only if the code doesn't already exist
IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = N'CASHFLOW_NEGATIVE_BALANCE_RISK')
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority], [IsActive], [CreatedAtUtc])
    VALUES (N'CASHFLOW_NEGATIVE_BALANCE_RISK', 2, 1, 3, 1, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = N'CASHFLOW_CASH_SHORTAGE_WARNING')
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority], [IsActive], [CreatedAtUtc])
    VALUES (N'CASHFLOW_CASH_SHORTAGE_WARNING', 2, 1, 2, 1, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = N'CASHFLOW_GOAL_AT_RISK')
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority], [IsActive], [CreatedAtUtc])
    VALUES (N'CASHFLOW_GOAL_AT_RISK', 2, 1, 2, 1, GETUTCDATE());
GO

-- Translations
INSERT INTO [MyMoney].[NotificationTemplateTranslations] ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
SELECT t.[TemplateId], 'en',
    N'Projected Negative Balance in {MonthName}',
    N'Your forecasted balance may reach {Amount} in {MonthName}. Review your spending plan to avoid a deficit.'
FROM [MyMoney].[NotificationTemplates] t WHERE t.[Code] = N'CASHFLOW_NEGATIVE_BALANCE_RISK'
  AND NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplateTranslations] x WHERE x.[TemplateId] = t.[TemplateId] AND x.[LanguageCode] = 'en');

INSERT INTO [MyMoney].[NotificationTemplateTranslations] ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
SELECT t.[TemplateId], 'ar',
    N'رصيد سالب متوقع في {MonthName}',
    N'قد يصل رصيدك المتوقع إلى {Amount} في {MonthName}. راجع خطة إنفاقك لتجنب العجز.'
FROM [MyMoney].[NotificationTemplates] t WHERE t.[Code] = N'CASHFLOW_NEGATIVE_BALANCE_RISK'
  AND NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplateTranslations] x WHERE x.[TemplateId] = t.[TemplateId] AND x.[LanguageCode] = 'ar');

INSERT INTO [MyMoney].[NotificationTemplateTranslations] ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
SELECT t.[TemplateId], 'en',
    N'Low Cash Reserve Warning',
    N'Your projected balance in {MonthName} ({Amount}) may fall below a 3-month safety buffer. Consider building your reserve.'
FROM [MyMoney].[NotificationTemplates] t WHERE t.[Code] = N'CASHFLOW_CASH_SHORTAGE_WARNING'
  AND NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplateTranslations] x WHERE x.[TemplateId] = t.[TemplateId] AND x.[LanguageCode] = 'en');

INSERT INTO [MyMoney].[NotificationTemplateTranslations] ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
SELECT t.[TemplateId], 'ar',
    N'تحذير: احتياطي نقدي منخفض',
    N'قد ينخفض رصيدك المتوقع في {MonthName} ({Amount}) عن مخزن أمان 3 أشهر. فكّر في بناء احتياطيك.'
FROM [MyMoney].[NotificationTemplates] t WHERE t.[Code] = N'CASHFLOW_CASH_SHORTAGE_WARNING'
  AND NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplateTranslations] x WHERE x.[TemplateId] = t.[TemplateId] AND x.[LanguageCode] = 'ar');

INSERT INTO [MyMoney].[NotificationTemplateTranslations] ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
SELECT t.[TemplateId], 'en',
    N'Goal at Risk: {GoalName}',
    N'Your current pace ({CurrentPace}/mo) is below the required contribution of {RequiredPace}/mo to reach "{GoalName}" on time.'
FROM [MyMoney].[NotificationTemplates] t WHERE t.[Code] = N'CASHFLOW_GOAL_AT_RISK'
  AND NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplateTranslations] x WHERE x.[TemplateId] = t.[TemplateId] AND x.[LanguageCode] = 'en');

INSERT INTO [MyMoney].[NotificationTemplateTranslations] ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
SELECT t.[TemplateId], 'ar',
    N'هدف في خطر: {GoalName}',
    N'إيقاعك الحالي ({CurrentPace}/شهر) أقل من المساهمة المطلوبة {RequiredPace}/شهر لتحقيق "{GoalName}" في الوقت المحدد.'
FROM [MyMoney].[NotificationTemplates] t WHERE t.[Code] = N'CASHFLOW_GOAL_AT_RISK'
  AND NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplateTranslations] x WHERE x.[TemplateId] = t.[TemplateId] AND x.[LanguageCode] = 'ar');
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- STORED PROCEDURES
-- ─────────────────────────────────────────────────────────────────────────────

-- 1. usp_CashFlow_GetComputationInputs
--    Returns 5 result sets: historical snapshots, active recurring defs,
--    active goals with contribution pace, category trends, cumulative balance.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_CashFlow_GetComputationInputs]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today       DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @HistoryStart DATE = DATEADD(MONTH, -12,
        DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1));

    -- RS1: Monthly snapshots (last 12 months, newest first)
    SELECT TOP 12
        s.[SnapshotDate],
        s.[TotalIncome],
        s.[TotalExpense],
        s.[NetBalance],
        s.[AverageDailySpend],
        s.[TransactionCount]
    FROM [MyMoney].[UserFinancialSnapshots] s
    WHERE s.[UserId]     = @UserId
      AND s.[PeriodType] = 2            -- Monthly
      AND s.[SnapshotDate] >= @HistoryStart
    ORDER BY s.[SnapshotDate] DESC;

    -- RS2: Active recurring definitions (joined with subscription metadata)
    SELECT
        rd.[Id]               AS RecurringId,
        rd.[TransactionTypeId],
        rd.[Amount],
        rd.[FrequencyId],
        rd.[FrequencyInterval],
        rd.[FrequencyUnit],
        rd.[DayOfMonth],
        rd.[DayOfWeek],
        rd.[StartDate],
        rd.[EndDate],
        rd.[IsSubscription],
        sm.[ProviderName],
        sm.[RenewalDate],
        ISNULL(sm.[AutoRenew], 0)  AS AutoRenew
    FROM [MyMoney].[RecurringTransactionDefinitions] rd
    LEFT JOIN [MyMoney].[SubscriptionMetadata] sm ON sm.[DefinitionId] = rd.[Id]
    WHERE rd.[UserId]   = @UserId
      AND rd.[StatusId] = 1            -- Active
      AND (rd.[EndDate] IS NULL OR rd.[EndDate] > @Today);

    -- RS3: Active goals + 3-month avg contribution pace
    SELECT
        g.[GoalId],
        g.[Name]          AS GoalName,
        g.[TargetAmount],
        g.[CurrentAmount],
        g.[TargetDate],
        ISNULL(contrib.[AvgMonthlyPace], 0) AS AvgMonthlyPace
    FROM [MyMoney].[Goals] g
    LEFT JOIN (
        SELECT
            gc.[GoalId],
            -- Net contributions (positive = saved, negative = withdrawn)
            SUM(gc.[Amount] * CASE WHEN gc.[IsDebit] = 0 THEN 1.0 ELSE -1.0 END) / 3.0
                AS AvgMonthlyPace
        FROM [MyMoney].[GoalContributions] gc
        WHERE gc.[UserId]           = @UserId
          AND gc.[ContributionDate] >= DATEADD(MONTH, -3, @Today)
        GROUP BY gc.[GoalId]
    ) contrib ON contrib.[GoalId] = g.[GoalId]
    WHERE g.[UserId]   = @UserId
      AND g.[StatusId] = 1;            -- Active

    -- RS4: Category spending trends (last complete month)
    DECLARE @LastMonthStart DATE = DATEFROMPARTS(
        YEAR(DATEADD(MONTH, -1, @Today)),
        MONTH(DATEADD(MONTH, -1, @Today)),
        1);

    SELECT
        csa.[CategoryId],
        csa.[TrendDirection],
        csa.[ChangePercentage]
    FROM [MyMoney].[CategorySpendingAnalytics] csa
    WHERE csa.[UserId]      = @UserId
      AND csa.[PeriodStart] = @LastMonthStart;

    -- RS5: Cumulative net balance estimate
    SELECT ISNULL(SUM(s.[NetBalance]), 0) AS CumulativeNetBalance
    FROM [MyMoney].[UserFinancialSnapshots] s
    WHERE s.[UserId]     = @UserId
      AND s.[PeriodType] = 2;
END
GO


-- 2. usp_CashFlow_Forecast_Upsert
--    UPSERT: replaces or creates the single CashFlowForecasts row for a user.
--    Returns the ForecastId via OUTPUT parameter.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_CashFlow_Forecast_Upsert]
    @UserId               BIGINT,
    @HorizonMonths        TINYINT,
    @MonthsOfHistoryUsed  TINYINT,
    @CurrentBalanceEst    DECIMAL(18,2),
    @OverallConfidence    DECIMAL(5,2),
    @ConfidenceBand       TINYINT,
    @RecurringIncomeMthly DECIMAL(18,2),
    @RecurringExpMthly    DECIMAL(18,2),
    @AvgVarIncomeMthly    DECIMAL(18,2),
    @AvgVarExpMthly       DECIMAL(18,2),
    @ForecastedEndBalance DECIMAL(18,2),
    @ForecastId           BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIME2(0) = GETUTCDATE();

    IF EXISTS (SELECT 1 FROM [MyMoney].[CashFlowForecasts] WHERE [UserId] = @UserId)
    BEGIN
        SELECT @ForecastId = [ForecastId]
        FROM   [MyMoney].[CashFlowForecasts]
        WHERE  [UserId] = @UserId;

        UPDATE [MyMoney].[CashFlowForecasts]
        SET [GeneratedAtUtc]       = @Now,
            [HorizonMonths]        = @HorizonMonths,
            [MonthsOfHistoryUsed]  = @MonthsOfHistoryUsed,
            [CurrentBalanceEst]    = @CurrentBalanceEst,
            [OverallConfidence]    = @OverallConfidence,
            [ConfidenceBand]       = @ConfidenceBand,
            [RecurringIncomeMthly] = @RecurringIncomeMthly,
            [RecurringExpMthly]    = @RecurringExpMthly,
            [AvgVarIncomeMthly]    = @AvgVarIncomeMthly,
            [AvgVarExpMthly]       = @AvgVarExpMthly,
            [ForecastedEndBalance] = @ForecastedEndBalance
        WHERE [ForecastId] = @ForecastId;
    END
    ELSE
    BEGIN
        INSERT INTO [MyMoney].[CashFlowForecasts]
            ([UserId], [GeneratedAtUtc], [HorizonMonths], [MonthsOfHistoryUsed],
             [CurrentBalanceEst], [OverallConfidence], [ConfidenceBand],
             [RecurringIncomeMthly], [RecurringExpMthly],
             [AvgVarIncomeMthly], [AvgVarExpMthly], [ForecastedEndBalance])
        VALUES
            (@UserId, @Now, @HorizonMonths, @MonthsOfHistoryUsed,
             @CurrentBalanceEst, @OverallConfidence, @ConfidenceBand,
             @RecurringIncomeMthly, @RecurringExpMthly,
             @AvgVarIncomeMthly, @AvgVarExpMthly, @ForecastedEndBalance);

        SET @ForecastId = SCOPE_IDENTITY();
    END
END
GO


-- 3. usp_CashFlow_MonthlyPoints_Replace
--    Deletes existing monthly points for a forecast then inserts from JSON.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_CashFlow_MonthlyPoints_Replace]
    @ForecastId BIGINT,
    @UserId     BIGINT,
    @PointsJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [MyMoney].[ForecastMonthlyPoints]
    WHERE [ForecastId] = @ForecastId;

    IF @PointsJson IS NULL OR @PointsJson = N'[]' RETURN;

    INSERT INTO [MyMoney].[ForecastMonthlyPoints]
        ([ForecastId], [UserId], [MonthYear],
         [ProjectedIncome], [ProjectedExpense], [ProjectedNet], [RunningBalance],
         [RecurringIncome], [RecurringExpense], [VariableIncome], [VariableExpense],
         [ConfidenceScore])
    SELECT
        @ForecastId, @UserId,
        CAST(j.[MonthYear]       AS DATE),
        j.[ProjectedIncome],
        j.[ProjectedExpense],
        j.[ProjectedNet],
        j.[RunningBalance],
        j.[RecurringIncome],
        j.[RecurringExpense],
        j.[VariableIncome],
        j.[VariableExpense],
        j.[ConfidenceScore]
    FROM OPENJSON(@PointsJson) WITH (
        [MonthYear]        NVARCHAR(20)  '$.MonthYear',
        [ProjectedIncome]  DECIMAL(18,2) '$.ProjectedIncome',
        [ProjectedExpense] DECIMAL(18,2) '$.ProjectedExpense',
        [ProjectedNet]     DECIMAL(18,2) '$.ProjectedNet',
        [RunningBalance]   DECIMAL(18,2) '$.RunningBalance',
        [RecurringIncome]  DECIMAL(18,2) '$.RecurringIncome',
        [RecurringExpense] DECIMAL(18,2) '$.RecurringExpense',
        [VariableIncome]   DECIMAL(18,2) '$.VariableIncome',
        [VariableExpense]  DECIMAL(18,2) '$.VariableExpense',
        [ConfidenceScore]  DECIMAL(5,2)  '$.ConfidenceScore'
    ) AS j;
END
GO


-- 4. usp_CashFlow_Risks_Replace
--    Deletes existing risks for a forecast then inserts from JSON.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_CashFlow_Risks_Replace]
    @ForecastId BIGINT,
    @UserId     BIGINT,
    @RisksJson  NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [MyMoney].[ForecastRisks]
    WHERE [ForecastId] = @ForecastId;

    IF @RisksJson IS NULL OR @RisksJson = N'[]' RETURN;

    INSERT INTO [MyMoney].[ForecastRisks]
        ([ForecastId], [UserId], [RiskType], [Severity],
         [TitleEn], [TitleAr], [DescriptionEn], [DescriptionAr],
         [AffectedMonthYear], [DataPointJson])
    SELECT
        @ForecastId, @UserId,
        j.[RiskType], j.[Severity],
        j.[TitleEn], j.[TitleAr],
        j.[DescriptionEn], j.[DescriptionAr],
        CASE WHEN j.[AffectedMonthYear] IS NOT NULL
             THEN CAST(j.[AffectedMonthYear] AS DATE)
             ELSE NULL END,
        j.[DataPointJson]
    FROM OPENJSON(@RisksJson) WITH (
        [RiskType]          TINYINT       '$.RiskType',
        [Severity]          TINYINT       '$.Severity',
        [TitleEn]           NVARCHAR(200) '$.TitleEn',
        [TitleAr]           NVARCHAR(200) '$.TitleAr',
        [DescriptionEn]     NVARCHAR(500) '$.DescriptionEn',
        [DescriptionAr]     NVARCHAR(500) '$.DescriptionAr',
        [AffectedMonthYear] NVARCHAR(20)  '$.AffectedMonthYear',
        [DataPointJson]     NVARCHAR(MAX) '$.DataPointJson'
    ) AS j;
END
GO


-- 5. usp_CashFlow_GoalProjections_Replace
--    Deletes existing goal projections for a forecast then inserts from JSON.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_CashFlow_GoalProjections_Replace]
    @ForecastId      BIGINT,
    @UserId          BIGINT,
    @ProjectionsJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [MyMoney].[ForecastGoalProjections]
    WHERE [ForecastId] = @ForecastId;

    IF @ProjectionsJson IS NULL OR @ProjectionsJson = N'[]' RETURN;

    INSERT INTO [MyMoney].[ForecastGoalProjections]
        ([ForecastId], [UserId], [GoalId], [GoalName],
         [TargetAmount], [CurrentAmount], [TargetDate],
         [RequiredMonthlyContr], [AvgMonthlyPace],
         [EstimatedComplDate], [IsAtRisk], [DaysToCompletion])
    SELECT
        @ForecastId, @UserId,
        j.[GoalId], j.[GoalName],
        j.[TargetAmount], j.[CurrentAmount],
        CASE WHEN j.[TargetDate] IS NOT NULL THEN CAST(j.[TargetDate] AS DATE) ELSE NULL END,
        j.[RequiredMonthlyContr], j.[AvgMonthlyPace],
        CASE WHEN j.[EstimatedComplDate] IS NOT NULL THEN CAST(j.[EstimatedComplDate] AS DATE) ELSE NULL END,
        j.[IsAtRisk], j.[DaysToCompletion]
    FROM OPENJSON(@ProjectionsJson) WITH (
        [GoalId]               BIGINT        '$.GoalId',
        [GoalName]             NVARCHAR(100) '$.GoalName',
        [TargetAmount]         DECIMAL(18,2) '$.TargetAmount',
        [CurrentAmount]        DECIMAL(18,2) '$.CurrentAmount',
        [TargetDate]           NVARCHAR(20)  '$.TargetDate',
        [RequiredMonthlyContr] DECIMAL(18,2) '$.RequiredMonthlyContr',
        [AvgMonthlyPace]       DECIMAL(18,2) '$.AvgMonthlyPace',
        [EstimatedComplDate]   NVARCHAR(20)  '$.EstimatedComplDate',
        [IsAtRisk]             BIT           '$.IsAtRisk',
        [DaysToCompletion]     INT           '$.DaysToCompletion'
    ) AS j;
END
GO


-- 6. usp_CashFlow_GetForecast
--    Returns 4 result sets: forecast header + monthly points + risks + goal projections.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_CashFlow_GetForecast]
    @UserId        BIGINT,
    @HorizonMonths TINYINT = 12
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ForecastId BIGINT;
    SELECT @ForecastId = [ForecastId]
    FROM   [MyMoney].[CashFlowForecasts]
    WHERE  [UserId] = @UserId;

    -- RS1: Forecast header
    SELECT
        f.[ForecastId],
        f.[GeneratedAtUtc],
        f.[HorizonMonths],
        f.[MonthsOfHistoryUsed],
        f.[CurrentBalanceEst],
        f.[OverallConfidence],
        f.[ConfidenceBand],
        f.[RecurringIncomeMthly],
        f.[RecurringExpMthly],
        f.[AvgVarIncomeMthly],
        f.[AvgVarExpMthly],
        f.[ForecastedEndBalance]
    FROM [MyMoney].[CashFlowForecasts] f
    WHERE f.[UserId] = @UserId;

    IF @ForecastId IS NULL
    BEGIN
        -- Return empty sets so the caller gets consistent result-set count
        SELECT TOP 0 * FROM [MyMoney].[ForecastMonthlyPoints] WHERE 1=0;
        SELECT TOP 0 * FROM [MyMoney].[ForecastRisks]         WHERE 1=0;
        SELECT TOP 0 * FROM [MyMoney].[ForecastGoalProjections] WHERE 1=0;
        RETURN;
    END

    DECLARE @EndDate DATE = DATEADD(MONTH, @HorizonMonths, CAST(GETUTCDATE() AS DATE));

    -- RS2: Monthly points filtered by requested horizon
    SELECT
        p.[PointId],
        p.[MonthYear],
        p.[ProjectedIncome],
        p.[ProjectedExpense],
        p.[ProjectedNet],
        p.[RunningBalance],
        p.[RecurringIncome],
        p.[RecurringExpense],
        p.[VariableIncome],
        p.[VariableExpense],
        p.[ConfidenceScore]
    FROM [MyMoney].[ForecastMonthlyPoints] p
    WHERE p.[ForecastId] = @ForecastId
      AND p.[MonthYear] <= @EndDate
    ORDER BY p.[MonthYear] ASC;

    -- RS3: All risks ordered by severity desc
    SELECT
        r.[RiskId],
        r.[RiskType],
        r.[Severity],
        r.[TitleEn],
        r.[TitleAr],
        r.[DescriptionEn],
        r.[DescriptionAr],
        r.[AffectedMonthYear],
        r.[DataPointJson]
    FROM [MyMoney].[ForecastRisks] r
    WHERE r.[ForecastId] = @ForecastId
    ORDER BY r.[Severity] DESC, r.[AffectedMonthYear] ASC;

    -- RS4: Goal projections ordered by at-risk first
    SELECT
        gp.[ProjectionId],
        gp.[GoalId],
        gp.[GoalName],
        gp.[TargetAmount],
        gp.[CurrentAmount],
        gp.[TargetDate],
        gp.[RequiredMonthlyContr],
        gp.[AvgMonthlyPace],
        gp.[EstimatedComplDate],
        gp.[IsAtRisk],
        gp.[DaysToCompletion]
    FROM [MyMoney].[ForecastGoalProjections] gp
    WHERE gp.[ForecastId] = @ForecastId
    ORDER BY gp.[IsAtRisk] DESC, gp.[TargetDate] ASC;
END
GO


-- 7. usp_CashFlow_GetDashboard
--    Lightweight read for the dashboard widget: header + next 3 months + top 3 risks.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_CashFlow_GetDashboard]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ForecastId  BIGINT;
    DECLARE @ThisMonthStart DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);

    SELECT @ForecastId = [ForecastId]
    FROM   [MyMoney].[CashFlowForecasts]
    WHERE  [UserId] = @UserId;

    -- RS1: Forecast summary
    SELECT
        f.[ForecastId],
        f.[GeneratedAtUtc],
        f.[OverallConfidence],
        f.[ConfidenceBand],
        f.[CurrentBalanceEst],
        f.[ForecastedEndBalance],
        f.[MonthsOfHistoryUsed],
        f.[RecurringIncomeMthly],
        f.[RecurringExpMthly]
    FROM [MyMoney].[CashFlowForecasts] f
    WHERE f.[UserId] = @UserId;

    IF @ForecastId IS NULL
    BEGIN
        SELECT TOP 0 1 AS Empty;
        SELECT TOP 0 1 AS Empty;
        RETURN;
    END

    -- RS2: Next 3 monthly points from current month
    SELECT TOP 3
        p.[MonthYear],
        p.[ProjectedIncome],
        p.[ProjectedExpense],
        p.[ProjectedNet],
        p.[RunningBalance],
        p.[ConfidenceScore]
    FROM [MyMoney].[ForecastMonthlyPoints] p
    WHERE p.[ForecastId] = @ForecastId
      AND p.[MonthYear]  >= @ThisMonthStart
    ORDER BY p.[MonthYear] ASC;

    -- RS3: Top 3 risks
    SELECT TOP 3
        r.[RiskId],
        r.[RiskType],
        r.[Severity],
        r.[TitleEn],
        r.[TitleAr],
        r.[DescriptionEn],
        r.[DescriptionAr],
        r.[AffectedMonthYear]
    FROM [MyMoney].[ForecastRisks] r
    WHERE r.[ForecastId] = @ForecastId
    ORDER BY r.[Severity] DESC, r.[AffectedMonthYear] ASC;
END
GO


-- 8. usp_CashFlow_Risk_MarkNotified
--    Stamps NotifiedAtUtc on a risk to prevent duplicate notifications.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_CashFlow_Risk_MarkNotified]
    @RiskId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [MyMoney].[ForecastRisks]
    SET [NotifiedAtUtc] = GETUTCDATE()
    WHERE [RiskId] = @RiskId;
END
GO


-- 9. usp_CashFlow_GetUnnotifiedRisks
--    Returns high-severity risks that have not yet been notified.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_CashFlow_GetUnnotifiedRisks]
    @UserId      BIGINT,
    @MinSeverity TINYINT = 2      -- default Medium and above
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ForecastId BIGINT;
    SELECT @ForecastId = [ForecastId]
    FROM   [MyMoney].[CashFlowForecasts]
    WHERE  [UserId] = @UserId;

    IF @ForecastId IS NULL RETURN;

    SELECT
        r.[RiskId],
        r.[RiskType],
        r.[Severity],
        r.[TitleEn],
        r.[TitleAr],
        r.[DescriptionEn],
        r.[DescriptionAr],
        r.[AffectedMonthYear],
        r.[DataPointJson]
    FROM [MyMoney].[ForecastRisks] r
    WHERE r.[ForecastId]    = @ForecastId
      AND r.[Severity]      >= @MinSeverity
      AND r.[NotifiedAtUtc] IS NULL
    ORDER BY r.[Severity] DESC;
END
GO


-- 10. usp_CashFlow_GetActiveUsers
--     Returns users who have had FIS snapshot activity in the last N days.
--     Reused by the daily scheduler to determine who needs forecast refresh.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_CashFlow_GetActiveUsers]
    @ActiveDays INT = 60
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT s.[UserId]
    FROM [MyMoney].[UserFinancialSnapshots] s
    WHERE s.[UpdatedAtUtc] >= DATEADD(DAY, -@ActiveDays, GETUTCDATE())
      AND s.[PeriodType]   = 2;  -- Monthly snapshots only
END
GO
