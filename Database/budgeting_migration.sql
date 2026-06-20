-- =============================================================================
-- Budgeting V2 Migration
-- =============================================================================
USE [MyMoney]
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- TABLE: Budgets
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'[MyMoney].[Budgets]', N'U') IS NULL
BEGIN
CREATE TABLE [MyMoney].[Budgets] (
    [BudgetId]     BIGINT        IDENTITY(1,1) NOT NULL,
    [UserId]       BIGINT        NOT NULL,
    [Name]         NVARCHAR(100) NOT NULL,
    [CategoryId]   INT           NULL,          -- NULL = overall spending budget
    [BudgetTypeId] TINYINT       NOT NULL,       -- 1=Fixed 2=Percentage 3=Annual 4=Flexible
    [Amount]       DECIMAL(18,2) NOT NULL,       -- fixed JOD, or % value (e.g. 15.00 = 15%)
    [PeriodTypeId] TINYINT       NOT NULL,       -- 1=Monthly 2=Quarterly 3=Yearly
    [StartDate]    DATE          NOT NULL,
    [EndDate]      DATE          NULL,
    [IsAutoRenew]  BIT           NOT NULL CONSTRAINT DF_Budgets_IsAutoRenew  DEFAULT(1),
    [StatusId]     TINYINT       NOT NULL CONSTRAINT DF_Budgets_StatusId     DEFAULT(1),  -- 1=Active 2=Paused 3=Archived
    [Notes]        NVARCHAR(500) NULL,
    [CreatedAtUtc] DATETIME2(0)  NOT NULL,
    [UpdatedAtUtc] DATETIME2(0)  NULL,
    CONSTRAINT PK_Budgets              PRIMARY KEY CLUSTERED ([BudgetId] ASC),
    CONSTRAINT CK_Budgets_BudgetTypeId CHECK ([BudgetTypeId] IN (1,2,3,4)),
    CONSTRAINT CK_Budgets_PeriodTypeId CHECK ([PeriodTypeId] IN (1,2,3)),
    CONSTRAINT CK_Budgets_StatusId     CHECK ([StatusId]     IN (1,2,3)),
    CONSTRAINT CK_Budgets_Amount       CHECK ([Amount]       >= 0)
) ON [PRIMARY];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Budgets_UserId' AND object_id = OBJECT_ID(N'[MyMoney].[Budgets]'))
    CREATE NONCLUSTERED INDEX [IX_Budgets_UserId]
        ON [MyMoney].[Budgets] ([UserId] ASC)
        INCLUDE ([StatusId], [CategoryId], [PeriodTypeId], [BudgetTypeId]);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- TABLE: BudgetPeriods
-- One row per calendar period per budget.
-- Active period is mutable (snapshot updated on each transaction write).
-- Closed periods are immutable historical records.
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'[MyMoney].[BudgetPeriods]', N'U') IS NULL
BEGIN
CREATE TABLE [MyMoney].[BudgetPeriods] (
    [PeriodId]             BIGINT        IDENTITY(1,1) NOT NULL,
    [BudgetId]             BIGINT        NOT NULL,
    [UserId]               BIGINT        NOT NULL,
    [PeriodStart]          DATE          NOT NULL,
    [PeriodEnd]            DATE          NOT NULL,
    -- Snapshot of the effective budget amount for this period
    [BudgetedAmount]       DECIMAL(18,2) NOT NULL CONSTRAINT DF_BudgetPeriods_BudgetedAmount DEFAULT(0),
    -- Computed metrics
    [ActualSpent]          DECIMAL(18,2) NOT NULL CONSTRAINT DF_BudgetPeriods_ActualSpent    DEFAULT(0),
    [UtilizationPct]       DECIMAL(5,2)  NOT NULL CONSTRAINT DF_BudgetPeriods_UtilPct        DEFAULT(0),
    [RemainingAmount]      DECIMAL(18,2) NOT NULL CONSTRAINT DF_BudgetPeriods_Remaining      DEFAULT(0),
    [OverBudgetAmount]     DECIMAL(18,2) NOT NULL CONSTRAINT DF_BudgetPeriods_OverBudget     DEFAULT(0),
    [ProjectedEndSpending] DECIMAL(18,2) NOT NULL CONSTRAINT DF_BudgetPeriods_Projected      DEFAULT(0),
    [DailyBudgetRemaining] DECIMAL(18,2) NULL,
    -- Risk & health
    [ForecastRiskId]       TINYINT       NOT NULL CONSTRAINT DF_BudgetPeriods_ForecastRisk   DEFAULT(1),  -- 1=Low 2=Medium 3=High
    [HealthScore]          TINYINT       NOT NULL CONSTRAINT DF_BudgetPeriods_HealthScore     DEFAULT(100),
    [HealthBandId]         TINYINT       NOT NULL CONSTRAINT DF_BudgetPeriods_HealthBand      DEFAULT(4),  -- 1=Poor 2=Fair 3=Good 4=Excellent
    -- Status & notifications
    [StatusId]             TINYINT       NOT NULL CONSTRAINT DF_BudgetPeriods_StatusId        DEFAULT(1),  -- 1=Active 2=Exceeded 3=Closed
    [Alert80PctSentAtUtc]  DATETIME2(0)  NULL,
    [Alert100PctSentAtUtc] DATETIME2(0)  NULL,
    -- Audit
    [ComputedAtUtc]        DATETIME2(0)  NULL,
    [ClosedAtUtc]          DATETIME2(0)  NULL,
    [CreatedAtUtc]         DATETIME2(0)  NOT NULL,
    CONSTRAINT PK_BudgetPeriods           PRIMARY KEY CLUSTERED ([PeriodId] ASC),
    CONSTRAINT UQ_BudgetPeriods_BudgetPeriodStart UNIQUE NONCLUSTERED ([BudgetId] ASC, [PeriodStart] ASC),
    CONSTRAINT FK_BudgetPeriods_Budgets   FOREIGN KEY ([BudgetId]) REFERENCES [MyMoney].[Budgets] ([BudgetId]),
    CONSTRAINT CK_BudgetPeriods_ForecastRiskId CHECK ([ForecastRiskId] IN (1,2,3)),
    CONSTRAINT CK_BudgetPeriods_HealthBandId   CHECK ([HealthBandId]   IN (1,2,3,4)),
    CONSTRAINT CK_BudgetPeriods_StatusId       CHECK ([StatusId]       IN (1,2,3))
) ON [PRIMARY];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BudgetPeriods_UserId_PeriodStart' AND object_id = OBJECT_ID(N'[MyMoney].[BudgetPeriods]'))
    CREATE NONCLUSTERED INDEX [IX_BudgetPeriods_UserId_PeriodStart]
        ON [MyMoney].[BudgetPeriods] ([UserId] ASC, [PeriodStart] ASC)
        INCLUDE ([BudgetId], [StatusId], [ActualSpent], [BudgetedAmount]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BudgetPeriods_BudgetId_Status' AND object_id = OBJECT_ID(N'[MyMoney].[BudgetPeriods]'))
    CREATE NONCLUSTERED INDEX [IX_BudgetPeriods_BudgetId_Status]
        ON [MyMoney].[BudgetPeriods] ([BudgetId] ASC, [StatusId] ASC)
        INCLUDE ([PeriodStart], [PeriodEnd], [ActualSpent], [BudgetedAmount], [HealthScore]);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- NOTIFICATION TEMPLATES: Budget seeds
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = N'BudgetNearingLimit')
BEGIN
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority], [IsActive], [CreatedAtUtc])
    VALUES (N'BudgetNearingLimit', 2, 1, 2, 1, SYSUTCDATETIME());

    DECLARE @tmpl1 INT = SCOPE_IDENTITY();
    INSERT INTO [MyMoney].[NotificationTemplateTranslations] ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
    VALUES
        (@tmpl1, N'en', N'Budget Alert: {{budgetName}}', N'You have used {{utilization}}% of your {{budgetName}} budget. {{remaining}} {{currency}} remaining.'),
        (@tmpl1, N'ar', N'تنبيه الميزانية: {{budgetName}}', N'لقد استخدمت {{utilization}}% من ميزانية {{budgetName}}. المتبقي {{remaining}} {{currency}}.');
END
GO

IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = N'BudgetExceeded')
BEGIN
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority], [IsActive], [CreatedAtUtc])
    VALUES (N'BudgetExceeded', 2, 1, 1, 1, SYSUTCDATETIME());

    DECLARE @tmpl2 INT = SCOPE_IDENTITY();
    INSERT INTO [MyMoney].[NotificationTemplateTranslations] ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
    VALUES
        (@tmpl2, N'en', N'Budget Exceeded: {{budgetName}}', N'You have exceeded your {{budgetName}} budget by {{overAmount}} {{currency}}.'),
        (@tmpl2, N'ar', N'تجاوز الميزانية: {{budgetName}}', N'لقد تجاوزت ميزانية {{budgetName}} بمقدار {{overAmount}} {{currency}}.');
END
GO

IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = N'BudgetPeriodReset')
BEGIN
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority], [IsActive], [CreatedAtUtc])
    VALUES (N'BudgetPeriodReset', 2, 1, 3, 1, SYSUTCDATETIME());

    DECLARE @tmpl3 INT = SCOPE_IDENTITY();
    INSERT INTO [MyMoney].[NotificationTemplateTranslations] ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
    VALUES
        (@tmpl3, N'en', N'Budget Reset: {{budgetName}}', N'Your {{budgetName}} budget has been reset for the new period. Budget: {{budgetAmount}} {{currency}}.'),
        (@tmpl3, N'ar', N'إعادة تعيين الميزانية: {{budgetName}}', N'تم إعادة تعيين ميزانية {{budgetName}} للفترة الجديدة. الميزانية: {{budgetAmount}} {{currency}}.');
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- HELPER: compute period start/end dates
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Budget_GetPeriodDates]
    @AsOfDate     DATE,
    @PeriodTypeId TINYINT,
    @PeriodStart  DATE OUTPUT,
    @PeriodEnd    DATE OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF @PeriodTypeId = 1  -- Monthly
    BEGIN
        SET @PeriodStart = DATEFROMPARTS(YEAR(@AsOfDate), MONTH(@AsOfDate), 1);
        SET @PeriodEnd   = EOMONTH(@AsOfDate);
    END
    ELSE IF @PeriodTypeId = 2  -- Quarterly
    BEGIN
        DECLARE @Quarter TINYINT = CEILING(MONTH(@AsOfDate) / 3.0);
        SET @PeriodStart = DATEFROMPARTS(YEAR(@AsOfDate), (@Quarter - 1) * 3 + 1, 1);
        SET @PeriodEnd   = EOMONTH(DATEADD(MONTH, 2, @PeriodStart));
    END
    ELSE  -- Yearly (PeriodTypeId = 3)
    BEGIN
        SET @PeriodStart = DATEFROMPARTS(YEAR(@AsOfDate), 1, 1);
        SET @PeriodEnd   = DATEFROMPARTS(YEAR(@AsOfDate), 12, 31);
    END
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_Budget_Create
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Budget_Create]
    @UserId        BIGINT,
    @Name          NVARCHAR(100),
    @CategoryId    INT           = NULL,
    @BudgetTypeId  TINYINT,
    @Amount        DECIMAL(18,2),
    @PeriodTypeId  TINYINT,
    @StartDate     DATE,
    @EndDate       DATE          = NULL,
    @IsAutoRenew   BIT,
    @Notes         NVARCHAR(500) = NULL,
    @NewBudgetId   BIGINT        OUTPUT,
    @ResultCode    TINYINT       OUTPUT   -- 0=Success 1=DuplicateBudget 2=InvalidCategory
AS
BEGIN
    SET NOCOUNT ON;
    SET @NewBudgetId = 0;
    SET @ResultCode  = 0;

    -- Validate CategoryId
    IF @CategoryId IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM [MyMoney].[Categories] WHERE CategoryId = @CategoryId AND IsActive = 1
    )
    BEGIN
        SET @ResultCode = 2;
        RETURN;
    END

    -- Prevent duplicate active/paused budget for same user + category (NULL = overall) + period type
    IF EXISTS (
        SELECT 1 FROM [MyMoney].[Budgets]
        WHERE  UserId       = @UserId
          AND  PeriodTypeId = @PeriodTypeId
          AND  StatusId    IN (1, 2)
          AND  ((@CategoryId IS NULL     AND CategoryId IS NULL)
             OR (CategoryId = @CategoryId))
    )
    BEGIN
        SET @ResultCode = 1;
        RETURN;
    END

    BEGIN TRANSACTION;
    BEGIN TRY
        INSERT INTO [MyMoney].[Budgets]
            (UserId, Name, CategoryId, BudgetTypeId, Amount, PeriodTypeId,
             StartDate, EndDate, IsAutoRenew, StatusId, Notes, CreatedAtUtc)
        VALUES
            (@UserId, @Name, @CategoryId, @BudgetTypeId, @Amount, @PeriodTypeId,
             @StartDate, @EndDate, @IsAutoRenew, 1, @Notes, SYSUTCDATETIME());

        SET @NewBudgetId = SCOPE_IDENTITY();

        -- Determine the first period dates (use today if StartDate is in the past)
        DECLARE @AsOfDate DATE = CASE
            WHEN @StartDate > CAST(GETUTCDATE() AS DATE) THEN @StartDate
            ELSE CAST(GETUTCDATE() AS DATE)
        END;

        DECLARE @PeriodStart DATE, @PeriodEnd DATE;
        EXEC [MyMoney].[usp_Budget_GetPeriodDates]
            @AsOfDate     = @AsOfDate,
            @PeriodTypeId = @PeriodTypeId,
            @PeriodStart  = @PeriodStart OUTPUT,
            @PeriodEnd    = @PeriodEnd   OUTPUT;

        -- For Percentage-based budgets the effective amount will be recomputed
        -- by usp_BudgetPeriod_ComputeSnapshot; store 0 initially.
        DECLARE @EffectiveAmount DECIMAL(18,2) =
            CASE WHEN @BudgetTypeId = 2 THEN 0 ELSE @Amount END;

        INSERT INTO [MyMoney].[BudgetPeriods]
            (BudgetId, UserId, PeriodStart, PeriodEnd, BudgetedAmount,
             ActualSpent, UtilizationPct, RemainingAmount, OverBudgetAmount,
             ProjectedEndSpending, DailyBudgetRemaining, ForecastRiskId,
             HealthScore, HealthBandId, StatusId, CreatedAtUtc)
        VALUES
            (@NewBudgetId, @UserId, @PeriodStart, @PeriodEnd, @EffectiveAmount,
             0, 0, @EffectiveAmount, 0,
             0, NULL, 1,
             100, 4, 1, SYSUTCDATETIME());

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_Budget_Update
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Budget_Update]
    @UserId        BIGINT,
    @BudgetId      BIGINT,
    @Name          NVARCHAR(100),
    @Amount        DECIMAL(18,2),
    @EndDate       DATE          = NULL,
    @IsAutoRenew   BIT,
    @Notes         NVARCHAR(500) = NULL,
    @AffectedRows  INT           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @AffectedRows = 0;

    -- Ownership check
    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[Budgets]
        WHERE BudgetId = @BudgetId AND UserId = @UserId AND StatusId != 3
    )
        RETURN;

    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE [MyMoney].[Budgets]
        SET    Name          = @Name,
               Amount        = @Amount,
               EndDate       = @EndDate,
               IsAutoRenew   = @IsAutoRenew,
               Notes         = @Notes,
               UpdatedAtUtc  = SYSUTCDATETIME()
        WHERE  BudgetId = @BudgetId AND UserId = @UserId;

        SET @AffectedRows = @@ROWCOUNT;

        -- Propagate amount change to the current active period
        IF @AffectedRows > 0
        BEGIN
            DECLARE @BudgetTypeId TINYINT;
            SELECT @BudgetTypeId = BudgetTypeId FROM [MyMoney].[Budgets] WHERE BudgetId = @BudgetId;

            -- Only propagate for non-percentage types (percentage is recomputed from income)
            IF @BudgetTypeId != 2
            BEGIN
                UPDATE [MyMoney].[BudgetPeriods]
                SET    BudgetedAmount  = @Amount,
                       RemainingAmount = @Amount - ActualSpent,
                       UtilizationPct  = CASE WHEN @Amount > 0 THEN ActualSpent / @Amount * 100 ELSE 0 END
                WHERE  BudgetId = @BudgetId AND StatusId IN (1, 2);
            END
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_Budget_UpdateStatus
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Budget_UpdateStatus]
    @UserId       BIGINT,
    @BudgetId     BIGINT,
    @NewStatusId  TINYINT,   -- 1=Active 2=Paused 3=Archived
    @AffectedRows INT        OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @AffectedRows = 0;

    UPDATE [MyMoney].[Budgets]
    SET    StatusId     = @NewStatusId,
           UpdatedAtUtc = SYSUTCDATETIME()
    WHERE  BudgetId = @BudgetId AND UserId = @UserId;

    SET @AffectedRows = @@ROWCOUNT;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_Budget_Delete
-- Archives if there are historical closed periods; hard-deletes otherwise.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Budget_Delete]
    @UserId       BIGINT,
    @BudgetId     BIGINT,
    @AffectedRows INT    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @AffectedRows = 0;

    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[Budgets]
        WHERE BudgetId = @BudgetId AND UserId = @UserId
    )
        RETURN;

    -- If there are closed historical periods → archive (preserve history)
    IF EXISTS (
        SELECT 1 FROM [MyMoney].[BudgetPeriods]
        WHERE BudgetId = @BudgetId AND StatusId = 3  -- Closed
    )
    BEGIN
        UPDATE [MyMoney].[Budgets]
        SET    StatusId = 3, UpdatedAtUtc = SYSUTCDATETIME()
        WHERE  BudgetId = @BudgetId AND UserId = @UserId;
    END
    ELSE
    BEGIN
        -- No history: hard delete both period and budget
        DELETE FROM [MyMoney].[BudgetPeriods] WHERE BudgetId = @BudgetId;
        DELETE FROM [MyMoney].[Budgets]       WHERE BudgetId = @BudgetId AND UserId = @UserId;
    END

    SET @AffectedRows = @@ROWCOUNT;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_Budget_GetList
-- Returns all budgets for a user with their current active period state.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Budget_GetList]
    @UserId   BIGINT,
    @StatusId TINYINT = NULL   -- NULL = all active/paused
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        b.BudgetId,
        b.Name,
        b.CategoryId,
        c.NameEn          AS CategoryNameEn,
        c.NameAr          AS CategoryNameAr,
        c.IconFileName    AS CategoryIcon,
        b.BudgetTypeId,
        b.Amount,
        b.PeriodTypeId,
        b.StartDate,
        b.EndDate,
        b.IsAutoRenew,
        b.StatusId,
        b.Notes,
        b.CreatedAtUtc,
        -- Current active period snapshot
        p.PeriodId,
        p.PeriodStart,
        p.PeriodEnd,
        p.BudgetedAmount,
        p.ActualSpent,
        p.UtilizationPct,
        p.RemainingAmount,
        p.OverBudgetAmount,
        p.ProjectedEndSpending,
        p.DailyBudgetRemaining,
        p.ForecastRiskId,
        p.HealthScore,
        p.HealthBandId,
        p.StatusId        AS PeriodStatusId
    FROM  [MyMoney].[Budgets] b
    LEFT  JOIN [MyMoney].[BudgetPeriods] p
           ON  p.BudgetId = b.BudgetId
           AND p.StatusId IN (1, 2)   -- Active or Exceeded
    LEFT  JOIN [MyMoney].[Categories] c ON c.CategoryId = b.CategoryId
    WHERE b.UserId = @UserId
      AND (
            @StatusId IS NULL
            OR b.StatusId = @StatusId
         )
      AND b.StatusId IN (1, 2, 3)   -- include all (filter via param)
    ORDER BY
        b.StatusId ASC,             -- active first
        CASE WHEN p.UtilizationPct >= 100 THEN 0 ELSE 1 END ASC,  -- exceeded first within group
        p.UtilizationPct DESC,
        b.CreatedAtUtc DESC;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_Budget_GetById
-- Returns budget detail + period history (multi-result set).
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Budget_GetById]
    @UserId   BIGINT,
    @BudgetId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: Budget definition + current period
    SELECT
        b.BudgetId,
        b.Name,
        b.CategoryId,
        c.NameEn          AS CategoryNameEn,
        c.NameAr          AS CategoryNameAr,
        c.IconFileName    AS CategoryIcon,
        b.BudgetTypeId,
        b.Amount,
        b.PeriodTypeId,
        b.StartDate,
        b.EndDate,
        b.IsAutoRenew,
        b.StatusId,
        b.Notes,
        b.CreatedAtUtc,
        b.UpdatedAtUtc,
        p.PeriodId,
        p.PeriodStart,
        p.PeriodEnd,
        p.BudgetedAmount,
        p.ActualSpent,
        p.UtilizationPct,
        p.RemainingAmount,
        p.OverBudgetAmount,
        p.ProjectedEndSpending,
        p.DailyBudgetRemaining,
        p.ForecastRiskId,
        p.HealthScore,
        p.HealthBandId,
        p.StatusId        AS PeriodStatusId,
        p.ComputedAtUtc
    FROM  [MyMoney].[Budgets] b
    LEFT  JOIN [MyMoney].[BudgetPeriods] p
           ON  p.BudgetId = b.BudgetId
           AND p.StatusId IN (1, 2)
    LEFT  JOIN [MyMoney].[Categories] c ON c.CategoryId = b.CategoryId
    WHERE b.BudgetId = @BudgetId AND b.UserId = @UserId;

    -- Result set 2: Last 12 closed periods for history/trend
    SELECT TOP 12
        p.PeriodId,
        p.PeriodStart,
        p.PeriodEnd,
        p.BudgetedAmount,
        p.ActualSpent,
        p.UtilizationPct,
        p.OverBudgetAmount,
        p.HealthScore,
        p.HealthBandId,
        p.ClosedAtUtc
    FROM [MyMoney].[BudgetPeriods] p
    WHERE p.BudgetId = @BudgetId
      AND p.UserId   = @UserId
      AND p.StatusId = 3  -- Closed
    ORDER BY p.PeriodStart DESC;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_Budget_GetDashboard
-- Dashboard overview: health summary + active budgets + over-budget list.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Budget_GetDashboard]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: Overall health summary
    SELECT
        COUNT(b.BudgetId)                                           AS TotalBudgets,
        SUM(CASE WHEN p.UtilizationPct >= 100 THEN 1 ELSE 0 END)   AS ExceededCount,
        SUM(CASE WHEN p.UtilizationPct >= 80
                  AND p.UtilizationPct < 100 THEN 1 ELSE 0 END)    AS NearLimitCount,
        SUM(CASE WHEN p.UtilizationPct <  80 THEN 1 ELSE 0 END)    AS OnTrackCount,
        CAST(AVG(CAST(p.HealthScore AS DECIMAL(5,2))) AS TINYINT)   AS OverallHealthScore,
        ISNULL(SUM(p.RemainingAmount), 0)                           AS TotalRemainingAmount,
        ISNULL(SUM(p.BudgetedAmount), 0)                            AS TotalBudgetedAmount,
        ISNULL(SUM(p.ActualSpent), 0)                               AS TotalActualSpent
    FROM  [MyMoney].[Budgets] b
    JOIN  [MyMoney].[BudgetPeriods] p
           ON  p.BudgetId = b.BudgetId
           AND p.StatusId IN (1, 2)
    WHERE b.UserId   = @UserId
      AND b.StatusId = 1;  -- Active only

    -- Result set 2: Active budget cards (ordered by utilization descending)
    SELECT
        b.BudgetId,
        b.Name,
        b.CategoryId,
        c.NameEn            AS CategoryNameEn,
        c.NameAr            AS CategoryNameAr,
        c.IconFileName      AS CategoryIcon,
        b.BudgetTypeId,
        b.PeriodTypeId,
        p.PeriodStart,
        p.PeriodEnd,
        p.BudgetedAmount,
        p.ActualSpent,
        p.UtilizationPct,
        p.RemainingAmount,
        p.OverBudgetAmount,
        p.ProjectedEndSpending,
        p.DailyBudgetRemaining,
        p.ForecastRiskId,
        p.HealthScore,
        p.HealthBandId,
        p.StatusId          AS PeriodStatusId
    FROM  [MyMoney].[Budgets] b
    JOIN  [MyMoney].[BudgetPeriods] p
           ON  p.BudgetId = b.BudgetId
           AND p.StatusId IN (1, 2)
    LEFT  JOIN [MyMoney].[Categories] c ON c.CategoryId = b.CategoryId
    WHERE b.UserId   = @UserId
      AND b.StatusId = 1
    ORDER BY
        p.UtilizationPct DESC,
        b.CreatedAtUtc   DESC;

    -- Result set 3: 6-month utilization trend across all budgets
    SELECT
        p.PeriodStart,
        AVG(p.UtilizationPct)                                         AS AvgUtilizationPct,
        SUM(p.BudgetedAmount)                                          AS TotalBudgeted,
        SUM(p.ActualSpent)                                             AS TotalSpent,
        SUM(CASE WHEN p.UtilizationPct >= 100 THEN 1 ELSE 0 END)      AS ExceededCount,
        CAST(AVG(CAST(p.HealthScore AS DECIMAL(5,2))) AS TINYINT)      AS AvgHealthScore
    FROM  [MyMoney].[BudgetPeriods] p
    JOIN  [MyMoney].[Budgets] b ON b.BudgetId = p.BudgetId AND b.UserId = @UserId
    WHERE p.UserId       = @UserId
      AND p.StatusId     = 3  -- Closed periods only for trend
      AND p.PeriodStart >= DATEADD(MONTH, -6, CAST(GETUTCDATE() AS DATE))
    GROUP BY p.PeriodStart
    ORDER BY p.PeriodStart DESC;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_Budget_GetAnalytics
-- Historical period breakdown for a specific budget.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Budget_GetAnalytics]
    @UserId   BIGINT,
    @BudgetId BIGINT    = NULL,   -- NULL = all budgets
    @DateFrom DATE      = NULL,
    @DateTo   DATE      = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        b.BudgetId,
        b.Name           AS BudgetName,
        b.CategoryId,
        c.NameEn         AS CategoryNameEn,
        c.NameAr         AS CategoryNameAr,
        b.PeriodTypeId,
        p.PeriodId,
        p.PeriodStart,
        p.PeriodEnd,
        p.BudgetedAmount,
        p.ActualSpent,
        p.UtilizationPct,
        p.OverBudgetAmount,
        p.HealthScore,
        p.HealthBandId,
        p.ForecastRiskId,
        p.StatusId       AS PeriodStatusId,
        p.ClosedAtUtc
    FROM  [MyMoney].[BudgetPeriods] p
    JOIN  [MyMoney].[Budgets] b    ON b.BudgetId  = p.BudgetId
    LEFT  JOIN [MyMoney].[Categories] c ON c.CategoryId = b.CategoryId
    WHERE b.UserId    = @UserId
      AND (@BudgetId  IS NULL OR b.BudgetId  = @BudgetId)
      AND (@DateFrom  IS NULL OR p.PeriodStart >= @DateFrom)
      AND (@DateTo    IS NULL OR p.PeriodEnd   <= @DateTo)
    ORDER BY p.PeriodStart DESC, b.BudgetId ASC;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_BudgetPeriod_EnsureActive
-- Creates an active period for today's date if one doesn't exist.
-- Called before computing snapshots.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_BudgetPeriod_EnsureActive]
    @UserId   BIGINT,
    @BudgetId BIGINT,
    @PeriodId BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @PeriodId = 0;

    DECLARE @Today        DATE    = CAST(GETUTCDATE() AS DATE);
    DECLARE @PeriodTypeId TINYINT;
    DECLARE @Amount       DECIMAL(18,2);
    DECLARE @BudgetTypeId TINYINT;
    DECLARE @StatusId     TINYINT;

    SELECT @PeriodTypeId = PeriodTypeId,
           @Amount       = Amount,
           @BudgetTypeId = BudgetTypeId,
           @StatusId     = StatusId
    FROM  [MyMoney].[Budgets]
    WHERE BudgetId = @BudgetId AND UserId = @UserId;

    IF @StatusId IS NULL RETURN;  -- Budget not found

    -- Check if an active/exceeded period already exists covering today
    SELECT @PeriodId = PeriodId
    FROM  [MyMoney].[BudgetPeriods]
    WHERE BudgetId  = @BudgetId
      AND @Today    BETWEEN PeriodStart AND PeriodEnd
      AND StatusId IN (1, 2);

    IF @PeriodId > 0 RETURN;  -- Already exists

    -- Compute period dates
    DECLARE @PeriodStart DATE, @PeriodEnd DATE;
    EXEC [MyMoney].[usp_Budget_GetPeriodDates]
        @AsOfDate     = @Today,
        @PeriodTypeId = @PeriodTypeId,
        @PeriodStart  = @PeriodStart OUTPUT,
        @PeriodEnd    = @PeriodEnd   OUTPUT;

    DECLARE @EffectiveAmount DECIMAL(18,2) =
        CASE WHEN @BudgetTypeId = 2 THEN 0 ELSE @Amount END;

    INSERT INTO [MyMoney].[BudgetPeriods]
        (BudgetId, UserId, PeriodStart, PeriodEnd, BudgetedAmount,
         ActualSpent, UtilizationPct, RemainingAmount, OverBudgetAmount,
         ProjectedEndSpending, DailyBudgetRemaining, ForecastRiskId,
         HealthScore, HealthBandId, StatusId, CreatedAtUtc)
    VALUES
        (@BudgetId, @UserId, @PeriodStart, @PeriodEnd, @EffectiveAmount,
         0, 0, @EffectiveAmount, 0,
         0, NULL, 1,
         100, 4, 1, SYSUTCDATETIME());

    SET @PeriodId = SCOPE_IDENTITY();
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_BudgetPeriod_ComputeSnapshot
-- Core computation: aggregates actual spending, derives all metrics, updates period.
-- Returns alert flags so the caller (C# handler) can send notifications.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_BudgetPeriod_ComputeSnapshot]
    @UserId               BIGINT,
    @BudgetId             BIGINT,
    @Alert80PctTriggered  BIT    OUTPUT,
    @Alert100PctTriggered BIT    OUTPUT,
    @PeriodId             BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Alert80PctTriggered  = 0;
    SET @Alert100PctTriggered = 0;
    SET @PeriodId             = 0;

    -- Ensure period exists
    EXEC [MyMoney].[usp_BudgetPeriod_EnsureActive]
        @UserId   = @UserId,
        @BudgetId = @BudgetId,
        @PeriodId = @PeriodId OUTPUT;

    IF @PeriodId = 0 RETURN;

    -- Load budget + period data
    DECLARE @CategoryId    INT,
            @BudgetTypeId  TINYINT,
            @BudgetAmount  DECIMAL(18,2),
            @PeriodStart   DATE,
            @PeriodEnd     DATE,
            @Prev80Sent    DATETIME2(0),
            @Prev100Sent   DATETIME2(0);

    SELECT @CategoryId   = b.CategoryId,
           @BudgetTypeId = b.BudgetTypeId,
           @BudgetAmount = b.Amount,
           @PeriodStart  = p.PeriodStart,
           @PeriodEnd    = p.PeriodEnd,
           @Prev80Sent   = p.Alert80PctSentAtUtc,
           @Prev100Sent  = p.Alert100PctSentAtUtc
    FROM  [MyMoney].[Budgets] b
    JOIN  [MyMoney].[BudgetPeriods] p ON p.PeriodId = @PeriodId
    WHERE b.BudgetId = @BudgetId AND b.UserId = @UserId;

    -- Aggregate actual spending from Transactions
    DECLARE @ActualSpent DECIMAL(18,2);
    SELECT @ActualSpent = ISNULL(SUM(t.Amount), 0)
    FROM  [MyMoney].[Transactions] t
    WHERE t.UserId            = @UserId
      AND t.IsActive          = 1
      AND t.TransactionTypeId = 2   -- expense
      AND t.TransactionDate   BETWEEN @PeriodStart AND @PeriodEnd
      AND (@CategoryId IS NULL OR t.CategoryId = @CategoryId);

    -- For Percentage-based budgets: recompute effective budget from actual income
    IF @BudgetTypeId = 2
    BEGIN
        DECLARE @TotalIncome DECIMAL(18,2);
        SELECT @TotalIncome = ISNULL(SUM(Amount), 0)
        FROM  [MyMoney].[Transactions]
        WHERE UserId            = @UserId
          AND IsActive          = 1
          AND TransactionTypeId = 1   -- income
          AND TransactionDate   BETWEEN @PeriodStart AND @PeriodEnd;

        SET @BudgetAmount = @TotalIncome * @BudgetAmount / 100.0;
        -- BudgetAmount here now holds the effective JOD value; update stored amount
        UPDATE [MyMoney].[BudgetPeriods]
        SET    BudgetedAmount = @BudgetAmount
        WHERE  PeriodId = @PeriodId;
    END

    -- Derive metrics
    DECLARE @UtilizationPct     DECIMAL(5,2),
            @RemainingAmount    DECIMAL(18,2),
            @OverBudgetAmount   DECIMAL(18,2),
            @ProjectedEnd       DECIMAL(18,2),
            @DailyRemaining     DECIMAL(18,2),
            @ForecastRiskId     TINYINT,
            @HealthScore        TINYINT,
            @HealthBandId       TINYINT,
            @PeriodStatusId     TINYINT;

    SET @UtilizationPct   = CASE WHEN @BudgetAmount > 0
                                 THEN @ActualSpent / @BudgetAmount * 100.0
                                 ELSE 0 END;
    SET @RemainingAmount  = @BudgetAmount - @ActualSpent;
    SET @OverBudgetAmount = CASE WHEN @ActualSpent > @BudgetAmount
                                 THEN @ActualSpent - @BudgetAmount
                                 ELSE 0 END;

    DECLARE @Today         DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @TotalDays     INT  = DATEDIFF(DAY, @PeriodStart, @PeriodEnd) + 1;
    DECLARE @DaysElapsed   INT  = DATEDIFF(DAY, @PeriodStart, @Today) + 1;
    DECLARE @DaysRemaining INT  = DATEDIFF(DAY, @Today, @PeriodEnd);

    -- Clamp days elapsed to period bounds
    SET @DaysElapsed = CASE
        WHEN @DaysElapsed < 1         THEN 1
        WHEN @DaysElapsed > @TotalDays THEN @TotalDays
        ELSE @DaysElapsed
    END;

    SET @ProjectedEnd = CASE
        WHEN @DaysElapsed > 0 AND @TotalDays > 0
        THEN @ActualSpent / CAST(@DaysElapsed AS DECIMAL(18,4)) * @TotalDays
        ELSE @ActualSpent
    END;

    SET @DailyRemaining = CASE
        WHEN @DaysRemaining > 0 AND @RemainingAmount > 0
        THEN @RemainingAmount / @DaysRemaining
        ELSE NULL
    END;

    -- Forecast risk
    SET @ForecastRiskId = CASE
        WHEN @BudgetAmount > 0 AND @ProjectedEnd > @BudgetAmount * 1.20 THEN 3  -- High
        WHEN @BudgetAmount > 0 AND @ProjectedEnd > @BudgetAmount        THEN 2  -- Medium
        ELSE 1                                                                    -- Low
    END;

    -- Health score (0–100)
    -- Expected utilization based on time elapsed
    DECLARE @ExpectedPct DECIMAL(5,2) = CASE
        WHEN @TotalDays > 0 THEN CAST(@DaysElapsed AS DECIMAL(5,2)) / @TotalDays * 100.0
        ELSE 100.0
    END;
    DECLARE @PaceDeviation DECIMAL(5,2) = @UtilizationPct - @ExpectedPct;

    DECLARE @RawScore DECIMAL(5,2) =
        100.0
        - CASE WHEN @UtilizationPct >= 100 THEN 55.0 + (@UtilizationPct - 100) * 0.3 ELSE 0 END
        - CASE WHEN @PaceDeviation > 25    THEN (@PaceDeviation - 25) * 0.6            ELSE 0 END
        - CASE WHEN @ForecastRiskId = 3    THEN 12.0                                   ELSE 0 END
        - CASE WHEN @ForecastRiskId = 2    THEN 6.0                                    ELSE 0 END;

    SET @HealthScore = CAST(CASE
        WHEN @RawScore < 0   THEN 0
        WHEN @RawScore > 100 THEN 100
        ELSE @RawScore
    END AS TINYINT);

    SET @HealthBandId = CASE
        WHEN @HealthScore >= 80 THEN 4  -- Excellent
        WHEN @HealthScore >= 60 THEN 3  -- Good
        WHEN @HealthScore >= 35 THEN 2  -- Fair
        ELSE                        1   -- Poor
    END;

    -- Period status
    SET @PeriodStatusId = CASE WHEN @ActualSpent > @BudgetAmount THEN 2 ELSE 1 END;

    -- Notification threshold detection
    -- 80% alert: trigger only once per period
    IF @UtilizationPct >= 80 AND @UtilizationPct < 100 AND @Prev80Sent IS NULL
        SET @Alert80PctTriggered = 1;

    -- 100% alert: trigger only once per period
    IF @UtilizationPct >= 100 AND @Prev100Sent IS NULL
        SET @Alert100PctTriggered = 1;

    -- Update BudgetPeriods snapshot
    UPDATE [MyMoney].[BudgetPeriods]
    SET    ActualSpent          = @ActualSpent,
           UtilizationPct       = @UtilizationPct,
           RemainingAmount      = @RemainingAmount,
           OverBudgetAmount     = @OverBudgetAmount,
           ProjectedEndSpending = @ProjectedEnd,
           DailyBudgetRemaining = @DailyRemaining,
           ForecastRiskId       = @ForecastRiskId,
           HealthScore          = @HealthScore,
           HealthBandId         = @HealthBandId,
           StatusId             = @PeriodStatusId,
           Alert80PctSentAtUtc  = CASE WHEN @Alert80PctTriggered  = 1 THEN SYSUTCDATETIME() ELSE Alert80PctSentAtUtc  END,
           Alert100PctSentAtUtc = CASE WHEN @Alert100PctTriggered = 1 THEN SYSUTCDATETIME() ELSE Alert100PctSentAtUtc END,
           ComputedAtUtc        = SYSUTCDATETIME()
    WHERE  PeriodId = @PeriodId;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_BudgetPeriod_CloseExpired
-- Closes periods whose PeriodEnd < today and creates new periods for auto-renew
-- budgets. Called by the daily scheduler.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_BudgetPeriod_CloseExpired]
    @UserId              BIGINT    = NULL,  -- NULL = all users
    @PeriodsClosedCount  INT       OUTPUT,
    @PeriodsCreatedCount INT       OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @PeriodsClosedCount  = 0;
    SET @PeriodsCreatedCount = 0;

    DECLARE @Today DATE = CAST(GETUTCDATE() AS DATE);

    -- Close expired active/exceeded periods
    UPDATE [MyMoney].[BudgetPeriods]
    SET    StatusId    = 3,
           ClosedAtUtc = SYSUTCDATETIME()
    WHERE  StatusId  IN (1, 2)
      AND  PeriodEnd  <  @Today
      AND  (@UserId IS NULL OR UserId = @UserId);

    SET @PeriodsClosedCount = @@ROWCOUNT;

    -- Create new periods for auto-renew active budgets that have no current period
    DECLARE @BudgetCursor CURSOR;
    SET @BudgetCursor = CURSOR FAST_FORWARD FOR
        SELECT b.BudgetId, b.UserId, b.PeriodTypeId, b.BudgetTypeId, b.Amount
        FROM  [MyMoney].[Budgets] b
        WHERE b.StatusId  = 1  -- Active
          AND b.IsAutoRenew = 1
          AND (@UserId IS NULL OR b.UserId = @UserId)
          AND NOT EXISTS (
              SELECT 1 FROM [MyMoney].[BudgetPeriods] p
              WHERE p.BudgetId = b.BudgetId
                AND @Today BETWEEN p.PeriodStart AND p.PeriodEnd
                AND p.StatusId IN (1, 2)
          );

    DECLARE @CurBudgetId   BIGINT,
            @CurUserId     BIGINT,
            @CurPeriodType TINYINT,
            @CurBudgType   TINYINT,
            @CurAmount     DECIMAL(18,2);

    OPEN @BudgetCursor;
    FETCH NEXT FROM @BudgetCursor INTO @CurBudgetId, @CurUserId, @CurPeriodType, @CurBudgType, @CurAmount;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @NewPeriodStart DATE, @NewPeriodEnd DATE;
        EXEC [MyMoney].[usp_Budget_GetPeriodDates]
            @AsOfDate     = @Today,
            @PeriodTypeId = @CurPeriodType,
            @PeriodStart  = @NewPeriodStart OUTPUT,
            @PeriodEnd    = @NewPeriodEnd   OUTPUT;

        DECLARE @NewEffective DECIMAL(18,2) =
            CASE WHEN @CurBudgType = 2 THEN 0 ELSE @CurAmount END;

        INSERT INTO [MyMoney].[BudgetPeriods]
            (BudgetId, UserId, PeriodStart, PeriodEnd, BudgetedAmount,
             ActualSpent, UtilizationPct, RemainingAmount, OverBudgetAmount,
             ProjectedEndSpending, DailyBudgetRemaining, ForecastRiskId,
             HealthScore, HealthBandId, StatusId, CreatedAtUtc)
        VALUES
            (@CurBudgetId, @CurUserId, @NewPeriodStart, @NewPeriodEnd, @NewEffective,
             0, 0, @NewEffective, 0,
             0, NULL, 1,
             100, 4, 1, SYSUTCDATETIME());

        SET @PeriodsCreatedCount += 1;

        FETCH NEXT FROM @BudgetCursor INTO @CurBudgetId, @CurUserId, @CurPeriodType, @CurBudgType, @CurAmount;
    END

    CLOSE @BudgetCursor;
    DEALLOCATE @BudgetCursor;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_BudgetPeriod_GetList
-- Historical period list for a specific budget.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_BudgetPeriod_GetList]
    @UserId    BIGINT,
    @BudgetId  BIGINT,
    @PageNumber INT = 1,
    @PageSize   INT = 12
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    SELECT
        p.PeriodId,
        p.PeriodStart,
        p.PeriodEnd,
        p.BudgetedAmount,
        p.ActualSpent,
        p.UtilizationPct,
        p.RemainingAmount,
        p.OverBudgetAmount,
        p.ProjectedEndSpending,
        p.ForecastRiskId,
        p.HealthScore,
        p.HealthBandId,
        p.StatusId,
        p.ClosedAtUtc,
        p.CreatedAtUtc,
        COUNT(*) OVER() AS TotalCount
    FROM  [MyMoney].[BudgetPeriods] p
    WHERE p.BudgetId = @BudgetId
      AND p.UserId   = @UserId
    ORDER BY p.PeriodStart DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- PROCEDURE: usp_Budget_GetActiveUserIds
-- Returns distinct user IDs that have active budgets.
-- Used by the daily scheduler to process all users.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Budget_GetActiveUserIds]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT UserId
    FROM  [MyMoney].[Budgets]
    WHERE StatusId = 1;  -- Active
END
GO
