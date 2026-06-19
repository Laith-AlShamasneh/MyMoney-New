-- =============================================================================
-- Migration 016: Goals & Savings System
-- Date: 2026-06-19
-- Tables:   Goals, GoalContributions, GoalMilestones, GoalRecurringLinks
-- SPs (16): usp_Goal_Create/GetById/GetList/Update/SetStatus
--           usp_GoalContribution_Add/GetList/GetMonthlyStats
--           usp_GoalMilestone_GetByGoal/MarkNotified
--           usp_GoalRecurringLink_Upsert/Delete/GetByGoal
--           usp_Goal_GetDashboard/GetActiveForScheduleCheck
--           usp_Goal_GetPendingAutoContributions
--           usp_Goal_UpdateBehindScheduleNotified
-- Seeds:    3 notification templates
-- =============================================================================

-- =============================================================================
-- 1. TABLES
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Goals' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[Goals]
    (
        [GoalId]                      BIGINT        IDENTITY(1,1)  NOT NULL,
        [UserId]                      BIGINT                       NOT NULL,
        [Name]                        NVARCHAR(100)                NOT NULL,
        [Description]                 NVARCHAR(500)                NULL,
        [GoalTypeId]                  TINYINT                      NOT NULL,
        [TargetAmount]                DECIMAL(18,2)                NOT NULL,
        [CurrentAmount]               DECIMAL(18,2)                NOT NULL  CONSTRAINT [DF_Goals_CurrentAmount] DEFAULT 0,
        [TargetDate]                  DATE                         NULL,
        [Priority]                    TINYINT                      NOT NULL  CONSTRAINT [DF_Goals_Priority]     DEFAULT 2,
        [StatusId]                    TINYINT                      NOT NULL  CONSTRAINT [DF_Goals_StatusId]     DEFAULT 1,
        [Icon]                        NVARCHAR(50)                 NULL,
        [Color]                       NVARCHAR(10)                 NULL,
        [CreatedAtUtc]                DATETIME2(7)                 NOT NULL  CONSTRAINT [DF_Goals_CreatedAtUtc] DEFAULT GETUTCDATE(),
        [UpdatedAtUtc]                DATETIME2(7)                 NULL,
        [CompletedAtUtc]              DATETIME2(7)                 NULL,
        [BehindScheduleNotifiedAtUtc] DATETIME2(7)                 NULL,

        CONSTRAINT [PK_Goals]            PRIMARY KEY CLUSTERED ([GoalId] ASC),
        CONSTRAINT [FK_Goals_Users]      FOREIGN KEY ([UserId])   REFERENCES [MyMoney].[Users]([UserId]),
        CONSTRAINT [CK_Goals_TargetAmt]  CHECK ([TargetAmount]  >  0),
        CONSTRAINT [CK_Goals_CurrentAmt] CHECK ([CurrentAmount] >= 0),
        CONSTRAINT [CK_Goals_TypeId]     CHECK ([GoalTypeId]    BETWEEN 1 AND 8),
        CONSTRAINT [CK_Goals_Priority]   CHECK ([Priority]      BETWEEN 1 AND 4),
        CONSTRAINT [CK_Goals_StatusId]   CHECK ([StatusId]      BETWEEN 1 AND 4)
    );

    CREATE NONCLUSTERED INDEX [IX_Goals_UserId_Status]
        ON [MyMoney].[Goals] ([UserId], [StatusId]);

    CREATE NONCLUSTERED INDEX [IX_Goals_UserId_TargetDate]
        ON [MyMoney].[Goals] ([UserId], [TargetDate])
        WHERE [TargetDate] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GoalContributions' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[GoalContributions]
    (
        [ContributionId]      BIGINT        IDENTITY(1,1)  NOT NULL,
        [GoalId]              BIGINT                       NOT NULL,
        [UserId]              BIGINT                       NOT NULL,
        [ContributionTypeId]  TINYINT                      NOT NULL,
        [Amount]              DECIMAL(18,2)                NOT NULL,
        [IsDebit]             BIT                          NOT NULL  CONSTRAINT [DF_GC_IsDebit]     DEFAULT 0,
        [Notes]               NVARCHAR(500)                NULL,
        [ContributionDate]    DATE                         NOT NULL,
        [CreatedAtUtc]        DATETIME2(7)                 NOT NULL  CONSTRAINT [DF_GC_CreatedAtUtc] DEFAULT GETUTCDATE(),
        [SourceRecurringId]   BIGINT                       NULL,
        [LinkedTransactionId] BIGINT                       NULL,

        CONSTRAINT [PK_GoalContributions]        PRIMARY KEY CLUSTERED ([ContributionId] ASC),
        CONSTRAINT [FK_GC_Goals]                 FOREIGN KEY ([GoalId])               REFERENCES [MyMoney].[Goals]([GoalId]),
        CONSTRAINT [FK_GC_Users]                 FOREIGN KEY ([UserId])               REFERENCES [MyMoney].[Users]([UserId]),
        CONSTRAINT [FK_GC_RecurringDefinitions]  FOREIGN KEY ([SourceRecurringId])    REFERENCES [MyMoney].[RecurringTransactionDefinitions]([RecurringDefinitionId]),
        CONSTRAINT [FK_GC_Transactions]          FOREIGN KEY ([LinkedTransactionId])  REFERENCES [MyMoney].[Transactions]([TransactionId]),
        CONSTRAINT [CK_GC_Amount]                CHECK ([Amount] > 0),
        CONSTRAINT [CK_GC_TypeId]                CHECK ([ContributionTypeId] BETWEEN 1 AND 4)
    );

    CREATE NONCLUSTERED INDEX [IX_GoalContributions_GoalId_Date]
        ON [MyMoney].[GoalContributions] ([GoalId], [ContributionDate] DESC);

    CREATE NONCLUSTERED INDEX [IX_GoalContributions_UserId_Date]
        ON [MyMoney].[GoalContributions] ([UserId], [ContributionDate] DESC);

    CREATE NONCLUSTERED INDEX [IX_GoalContributions_SourceRecurringId]
        ON [MyMoney].[GoalContributions] ([SourceRecurringId])
        WHERE [SourceRecurringId] IS NOT NULL;

    CREATE NONCLUSTERED INDEX [IX_GoalContributions_LinkedTransactionId]
        ON [MyMoney].[GoalContributions] ([LinkedTransactionId])
        WHERE [LinkedTransactionId] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GoalMilestones' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[GoalMilestones]
    (
        [MilestoneId]      BIGINT        IDENTITY(1,1)  NOT NULL,
        [GoalId]           BIGINT                       NOT NULL,
        [UserId]           BIGINT                       NOT NULL,
        [MilestonePercent] TINYINT                      NOT NULL,
        [ReachedAtUtc]     DATETIME2(7)                 NOT NULL  CONSTRAINT [DF_GM_ReachedAtUtc] DEFAULT GETUTCDATE(),
        [NotifiedAtUtc]    DATETIME2(7)                 NULL,

        CONSTRAINT [PK_GoalMilestones]      PRIMARY KEY CLUSTERED ([MilestoneId] ASC),
        CONSTRAINT [FK_GM_Goals]            FOREIGN KEY ([GoalId]) REFERENCES [MyMoney].[Goals]([GoalId]),
        CONSTRAINT [UQ_GoalMilestones]      UNIQUE ([GoalId], [MilestonePercent]),
        CONSTRAINT [CK_GM_MilestonePercent] CHECK ([MilestonePercent] IN (25, 50, 75, 100))
    );

    CREATE NONCLUSTERED INDEX [IX_GoalMilestones_GoalId]
        ON [MyMoney].[GoalMilestones] ([GoalId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GoalRecurringLinks' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[GoalRecurringLinks]
    (
        [LinkId]                BIGINT        IDENTITY(1,1)  NOT NULL,
        [GoalId]                BIGINT                       NOT NULL,
        [RecurringDefinitionId] BIGINT                       NOT NULL,
        [UserId]                BIGINT                       NOT NULL,
        [CreatedAtUtc]          DATETIME2(7)                 NOT NULL  CONSTRAINT [DF_GRL_CreatedAtUtc] DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_GoalRecurringLinks]       PRIMARY KEY CLUSTERED ([LinkId] ASC),
        CONSTRAINT [FK_GRL_Goals]                FOREIGN KEY ([GoalId])               REFERENCES [MyMoney].[Goals]([GoalId]),
        CONSTRAINT [FK_GRL_RecurringDefinitions] FOREIGN KEY ([RecurringDefinitionId]) REFERENCES [MyMoney].[RecurringTransactionDefinitions]([RecurringDefinitionId]),
        CONSTRAINT [FK_GRL_Users]                FOREIGN KEY ([UserId])               REFERENCES [MyMoney].[Users]([UserId]),
        CONSTRAINT [UQ_GoalRecurringLinks]       UNIQUE ([GoalId], [RecurringDefinitionId])
    );

    CREATE NONCLUSTERED INDEX [IX_GoalRecurringLinks_RecurringDefinitionId]
        ON [MyMoney].[GoalRecurringLinks] ([RecurringDefinitionId]);
END
GO

-- =============================================================================
-- 2. STORED PROCEDURES
-- =============================================================================

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Goal_Create]
    @UserId        BIGINT,
    @Name          NVARCHAR(100),
    @Description   NVARCHAR(500)  = NULL,
    @GoalTypeId    TINYINT,
    @TargetAmount  DECIMAL(18,2),
    @InitialAmount DECIMAL(18,2)  = 0,
    @TargetDate    DATE           = NULL,
    @Priority      TINYINT        = 2,
    @Icon          NVARCHAR(50)   = NULL,
    @Color         NVARCHAR(10)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Initial DECIMAL(18,2) = ISNULL(@InitialAmount, 0);
    DECLARE @Now     DATETIME2(7)  = GETUTCDATE();
    DECLARE @GoalId  BIGINT;

    BEGIN TRAN;

    INSERT INTO [MyMoney].[Goals]
        ([UserId], [Name], [Description], [GoalTypeId], [TargetAmount], [CurrentAmount],
         [TargetDate], [Priority], [StatusId], [Icon], [Color], [CreatedAtUtc])
    VALUES
        (@UserId, @Name, @Description, @GoalTypeId, @TargetAmount, @Initial,
         @TargetDate, @Priority, 1, @Icon, @Color, @Now);

    SET @GoalId = SCOPE_IDENTITY();

    IF @Initial > 0
    BEGIN
        INSERT INTO [MyMoney].[GoalContributions]
            ([GoalId], [UserId], [ContributionTypeId], [Amount], [IsDebit], [Notes],
             [ContributionDate], [CreatedAtUtc])
        VALUES
            (@GoalId, @UserId, 1, @Initial, 0, N'Initial amount', CAST(@Now AS DATE), @Now);

        -- Check milestones reached by initial amount
        INSERT INTO [MyMoney].[GoalMilestones] ([GoalId], [UserId], [MilestonePercent], [ReachedAtUtc])
        SELECT @GoalId, @UserId, m.Pct, @Now
        FROM (VALUES (CAST(25 AS TINYINT)),(50),(75),(100)) AS m(Pct)
        WHERE CAST(m.Pct AS DECIMAL(8,2)) <= ROUND(CAST(@Initial AS DECIMAL(18,6)) / CAST(@TargetAmount AS DECIMAL(18,6)) * 100, 2)
          AND NOT EXISTS (
              SELECT 1 FROM [MyMoney].[GoalMilestones] gm
              WHERE gm.[GoalId] = @GoalId AND gm.[MilestonePercent] = m.Pct);
    END

    COMMIT TRAN;

    SELECT @GoalId AS [GoalId];
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Goal_GetById]
    @GoalId BIGINT,
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        g.[GoalId],
        g.[Name],
        g.[Description],
        g.[GoalTypeId],
        g.[TargetAmount],
        g.[CurrentAmount],
        g.[TargetDate],
        g.[Priority],
        g.[StatusId],
        g.[Icon],
        g.[Color],
        g.[CreatedAtUtc],
        g.[UpdatedAtUtc],
        g.[CompletedAtUtc],
        (SELECT COUNT(*) FROM [MyMoney].[GoalContributions] c
         WHERE c.[GoalId] = g.[GoalId])                                       AS [ContributionCount],
        (SELECT COUNT(*) FROM [MyMoney].[GoalRecurringLinks] l
         WHERE l.[GoalId] = g.[GoalId])                                       AS [LinkedRecurringCount]
    FROM [MyMoney].[Goals] g
    WHERE g.[GoalId]   = @GoalId
      AND g.[UserId]   = @UserId
      AND g.[StatusId] <> 4;
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Goal_GetList]
    @UserId     BIGINT,
    @StatusId   TINYINT = NULL,
    @GoalTypeId TINYINT = NULL,
    @Priority   TINYINT = NULL,
    @PageNumber INT     = 1,
    @PageSize   INT     = 20
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    SELECT COUNT(*) AS [TotalCount]
    FROM [MyMoney].[Goals] g
    WHERE g.[UserId]   = @UserId
      AND g.[StatusId] <> 4
      AND (@StatusId   IS NULL OR g.[StatusId]   = @StatusId)
      AND (@GoalTypeId IS NULL OR g.[GoalTypeId] = @GoalTypeId)
      AND (@Priority   IS NULL OR g.[Priority]   = @Priority);

    SELECT
        g.[GoalId],
        g.[Name],
        g.[Description],
        g.[GoalTypeId],
        g.[TargetAmount],
        g.[CurrentAmount],
        g.[TargetDate],
        g.[Priority],
        g.[StatusId],
        g.[Icon],
        g.[Color],
        g.[CreatedAtUtc],
        g.[CompletedAtUtc],
        CASE WHEN g.[TargetAmount] > 0
             THEN ROUND(CAST(g.[CurrentAmount] AS DECIMAL(18,6)) / CAST(g.[TargetAmount] AS DECIMAL(18,6)) * 100, 2)
             ELSE 0 END                                                        AS [CompletionPercent],
        (SELECT MAX(c.[ContributionDate]) FROM [MyMoney].[GoalContributions] c
         WHERE c.[GoalId] = g.[GoalId])                                        AS [LastContributionDate],
        (SELECT COUNT(*) FROM [MyMoney].[GoalRecurringLinks] l
         WHERE l.[GoalId] = g.[GoalId])                                        AS [LinkedRecurringCount]
    FROM [MyMoney].[Goals] g
    WHERE g.[UserId]   = @UserId
      AND g.[StatusId] <> 4
      AND (@StatusId   IS NULL OR g.[StatusId]   = @StatusId)
      AND (@GoalTypeId IS NULL OR g.[GoalTypeId] = @GoalTypeId)
      AND (@Priority   IS NULL OR g.[Priority]   = @Priority)
    ORDER BY
        g.[Priority] DESC,
        CASE WHEN g.[TargetDate] IS NOT NULL THEN g.[TargetDate]
             ELSE CAST('9999-12-31' AS DATE) END ASC,
        g.[CreatedAtUtc] DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Goal_Update]
    @GoalId       BIGINT,
    @UserId       BIGINT,
    @Name         NVARCHAR(100),
    @Description  NVARCHAR(500) = NULL,
    @TargetAmount DECIMAL(18,2),
    @TargetDate   DATE          = NULL,
    @Priority     TINYINT,
    @Icon         NVARCHAR(50)  = NULL,
    @Color        NVARCHAR(10)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[Goals]
    SET
        [Name]         = @Name,
        [Description]  = @Description,
        [TargetAmount] = @TargetAmount,
        [TargetDate]   = @TargetDate,
        [Priority]     = @Priority,
        [Icon]         = @Icon,
        [Color]        = @Color,
        [UpdatedAtUtc] = GETUTCDATE()
    WHERE [GoalId]   = @GoalId
      AND [UserId]   = @UserId
      AND [StatusId] IN (1, 2);

    SELECT @@ROWCOUNT AS [AffectedRows];
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Goal_SetStatus]
    @GoalId   BIGINT,
    @UserId   BIGINT,
    @StatusId TINYINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIME2(7) = GETUTCDATE();

    UPDATE [MyMoney].[Goals]
    SET
        [StatusId]       = @StatusId,
        [UpdatedAtUtc]   = @Now,
        [CompletedAtUtc] = CASE
            WHEN @StatusId = 3 AND [CompletedAtUtc] IS NULL THEN @Now
            WHEN @StatusId IN (1, 2)                        THEN NULL
            ELSE [CompletedAtUtc]
        END
    WHERE [GoalId] = @GoalId
      AND [UserId] = @UserId;

    SELECT @@ROWCOUNT AS [AffectedRows];
END
GO

-- Core contribution engine: handles add/withdraw/adjust atomically.
-- Milestones are detected inline and returned in result set 2.
-- Result set 1: outcome row  |  Result set 2: newly-reached milestone percents
CREATE OR ALTER PROCEDURE [MyMoney].[usp_GoalContribution_Add]
    @GoalId              BIGINT,
    @UserId              BIGINT,
    @ContributionTypeId  TINYINT,
    @Amount              DECIMAL(18,2),
    @IsDebit             BIT,
    @Notes               NVARCHAR(500) = NULL,
    @ContributionDate    DATE,
    @SourceRecurringId   BIGINT        = NULL,
    @LinkedTransactionId BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CurrentAmount DECIMAL(18,2);
    DECLARE @TargetAmount  DECIMAL(18,2);
    DECLARE @StatusId      TINYINT;
    DECLARE @GoalName      NVARCHAR(100);

    SELECT
        @CurrentAmount = [CurrentAmount],
        @TargetAmount  = [TargetAmount],
        @StatusId      = [StatusId],
        @GoalName      = [Name]
    FROM [MyMoney].[Goals]
    WHERE [GoalId] = @GoalId AND [UserId] = @UserId;

    IF @CurrentAmount IS NULL
    BEGIN
        SELECT CAST(-1 AS BIGINT) AS [ContributionId], CAST(0 AS DECIMAL(18,2)) AS [NewCurrentAmount],
               CAST(0 AS DECIMAL(8,2)) AS [NewCompletionPercent], CAST(0 AS BIT) AS [GoalCompleted],
               CAST(-1 AS INT) AS [ErrorCode], CAST('' AS NVARCHAR(100)) AS [GoalName];
        SELECT TOP 0 CAST(0 AS TINYINT) AS [MilestonePercent];
        RETURN;
    END

    IF @StatusId NOT IN (1, 2)
    BEGIN
        SELECT CAST(-1 AS BIGINT) AS [ContributionId], CAST(0 AS DECIMAL(18,2)) AS [NewCurrentAmount],
               CAST(0 AS DECIMAL(8,2)) AS [NewCompletionPercent], CAST(0 AS BIT) AS [GoalCompleted],
               CAST(-2 AS INT) AS [ErrorCode], CAST('' AS NVARCHAR(100)) AS [GoalName];
        SELECT TOP 0 CAST(0 AS TINYINT) AS [MilestonePercent];
        RETURN;
    END

    DECLARE @NewAmount DECIMAL(18,2);

    IF @IsDebit = 0
        SET @NewAmount = @CurrentAmount + @Amount;
    ELSE
    BEGIN
        IF @Amount > @CurrentAmount
        BEGIN
            SELECT CAST(-1 AS BIGINT) AS [ContributionId], CAST(0 AS DECIMAL(18,2)) AS [NewCurrentAmount],
                   CAST(0 AS DECIMAL(8,2)) AS [NewCompletionPercent], CAST(0 AS BIT) AS [GoalCompleted],
                   CAST(-3 AS INT) AS [ErrorCode], CAST('' AS NVARCHAR(100)) AS [GoalName];
            SELECT TOP 0 CAST(0 AS TINYINT) AS [MilestonePercent];
            RETURN;
        END
        SET @NewAmount = @CurrentAmount - @Amount;
    END

    DECLARE @Now            DATETIME2(7) = GETUTCDATE();
    DECLARE @ShouldComplete BIT          = 0;
    DECLARE @CompletionPct  DECIMAL(8,2) =
        CASE WHEN @TargetAmount > 0
             THEN ROUND(CAST(@NewAmount AS DECIMAL(18,6)) / CAST(@TargetAmount AS DECIMAL(18,6)) * 100, 2)
             ELSE 0 END;

    IF @IsDebit = 0 AND @ContributionTypeId IN (1, 4)
       AND @NewAmount >= @TargetAmount AND @StatusId = 1
        SET @ShouldComplete = 1;

    DECLARE @NewMilestones TABLE ([MilestonePercent] TINYINT);

    BEGIN TRAN;

    INSERT INTO [MyMoney].[GoalContributions]
        ([GoalId], [UserId], [ContributionTypeId], [Amount], [IsDebit], [Notes],
         [ContributionDate], [CreatedAtUtc], [SourceRecurringId], [LinkedTransactionId])
    VALUES
        (@GoalId, @UserId, @ContributionTypeId, @Amount, @IsDebit, @Notes,
         @ContributionDate, @Now, @SourceRecurringId, @LinkedTransactionId);

    DECLARE @ContributionId BIGINT = SCOPE_IDENTITY();

    UPDATE [MyMoney].[Goals]
    SET
        [CurrentAmount]  = @NewAmount,
        [UpdatedAtUtc]   = @Now,
        [StatusId]       = CASE WHEN @ShouldComplete = 1 THEN 3      ELSE [StatusId]       END,
        [CompletedAtUtc] = CASE WHEN @ShouldComplete = 1 AND [CompletedAtUtc] IS NULL
                                THEN @Now ELSE [CompletedAtUtc] END
    WHERE [GoalId] = @GoalId;

    INSERT INTO [MyMoney].[GoalMilestones] ([GoalId], [UserId], [MilestonePercent], [ReachedAtUtc])
    OUTPUT INSERTED.[MilestonePercent] INTO @NewMilestones
    SELECT @GoalId, @UserId, m.Pct, @Now
    FROM (VALUES (CAST(25 AS TINYINT)),(50),(75),(100)) AS m(Pct)
    WHERE CAST(m.Pct AS DECIMAL(8,2)) <= @CompletionPct
      AND NOT EXISTS (
          SELECT 1 FROM [MyMoney].[GoalMilestones] gm
          WHERE gm.[GoalId] = @GoalId AND gm.[MilestonePercent] = m.Pct);

    COMMIT TRAN;

    SELECT
        @ContributionId AS [ContributionId],
        @NewAmount      AS [NewCurrentAmount],
        @CompletionPct  AS [NewCompletionPercent],
        @ShouldComplete AS [GoalCompleted],
        CAST(0 AS INT)  AS [ErrorCode],
        @GoalName       AS [GoalName];

    SELECT [MilestonePercent] FROM @NewMilestones;
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_GoalContribution_GetList]
    @GoalId     BIGINT,
    @UserId     BIGINT,
    @PageNumber INT = 1,
    @PageSize   INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM [MyMoney].[Goals] WHERE [GoalId] = @GoalId AND [UserId] = @UserId)
    BEGIN
        SELECT 0 AS [TotalCount];
        SELECT TOP 0 [ContributionId] FROM [MyMoney].[GoalContributions];
        RETURN;
    END

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    SELECT COUNT(*) AS [TotalCount]
    FROM [MyMoney].[GoalContributions]
    WHERE [GoalId] = @GoalId;

    SELECT
        c.[ContributionId],
        c.[ContributionTypeId],
        c.[Amount],
        c.[IsDebit],
        c.[Notes],
        c.[ContributionDate],
        c.[CreatedAtUtc],
        c.[SourceRecurringId],
        c.[LinkedTransactionId]
    FROM [MyMoney].[GoalContributions] c
    WHERE c.[GoalId] = @GoalId
    ORDER BY c.[CreatedAtUtc] DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_GoalContribution_GetMonthlyStats]
    @GoalId     BIGINT,
    @UserId     BIGINT,
    @MonthsBack INT = 3
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDate DATE = CAST(DATEADD(MONTH, -@MonthsBack, GETUTCDATE()) AS DATE);

    SELECT
        YEAR (c.[ContributionDate]) AS [Year],
        MONTH(c.[ContributionDate]) AS [Month],
        SUM(CASE WHEN c.[IsDebit] = 0 THEN c.[Amount] ELSE 0 END) AS [TotalContributed],
        SUM(CASE WHEN c.[IsDebit] = 1 THEN c.[Amount] ELSE 0 END) AS [TotalWithdrawn]
    FROM [MyMoney].[GoalContributions] c
    WHERE c.[GoalId]              = @GoalId
      AND c.[UserId]              = @UserId
      AND c.[ContributionDate]    >= @FromDate
      AND c.[ContributionTypeId]  <> 3
    GROUP BY YEAR(c.[ContributionDate]), MONTH(c.[ContributionDate]);
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_GoalMilestone_GetByGoal]
    @GoalId BIGINT,
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        m.[MilestoneId],
        m.[MilestonePercent],
        m.[ReachedAtUtc],
        m.[NotifiedAtUtc]
    FROM [MyMoney].[GoalMilestones] m
    WHERE m.[GoalId] = @GoalId
      AND m.[UserId] = @UserId
    ORDER BY m.[MilestonePercent];
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_GoalMilestone_MarkNotified]
    @GoalId           BIGINT,
    @MilestonePercent TINYINT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[GoalMilestones]
    SET    [NotifiedAtUtc] = GETUTCDATE()
    WHERE  [GoalId]           = @GoalId
      AND  [MilestonePercent] = @MilestonePercent
      AND  [NotifiedAtUtc]    IS NULL;
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_GoalRecurringLink_Upsert]
    @GoalId                BIGINT,
    @UserId                BIGINT,
    @RecurringDefinitionId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[Goals]
        WHERE [GoalId] = @GoalId AND [UserId] = @UserId AND [StatusId] IN (1, 2))
    BEGIN
        SELECT CAST(0 AS BIT) AS [Success]; RETURN;
    END

    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[RecurringTransactionDefinitions]
        WHERE [RecurringDefinitionId] = @RecurringDefinitionId AND [UserId] = @UserId)
    BEGIN
        SELECT CAST(0 AS BIT) AS [Success]; RETURN;
    END

    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[GoalRecurringLinks]
        WHERE [GoalId] = @GoalId AND [RecurringDefinitionId] = @RecurringDefinitionId)
    BEGIN
        INSERT INTO [MyMoney].[GoalRecurringLinks]
            ([GoalId], [RecurringDefinitionId], [UserId], [CreatedAtUtc])
        VALUES
            (@GoalId, @RecurringDefinitionId, @UserId, GETUTCDATE());
    END

    SELECT CAST(1 AS BIT) AS [Success];
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_GoalRecurringLink_Delete]
    @GoalId                BIGINT,
    @UserId                BIGINT,
    @RecurringDefinitionId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [MyMoney].[GoalRecurringLinks]
    WHERE [GoalId]                = @GoalId
      AND [UserId]                = @UserId
      AND [RecurringDefinitionId] = @RecurringDefinitionId;

    SELECT @@ROWCOUNT AS [AffectedRows];
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_GoalRecurringLink_GetByGoal]
    @GoalId BIGINT,
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        l.[LinkId],
        l.[RecurringDefinitionId],
        r.[Name]     AS [RecurringName],
        r.[Amount]   AS [RecurringAmount],
        r.[FrequencyId],
        r.[StatusId] AS [RecurringStatusId],
        l.[CreatedAtUtc]
    FROM  [MyMoney].[GoalRecurringLinks] l
    INNER JOIN [MyMoney].[RecurringTransactionDefinitions] r
           ON  r.[RecurringDefinitionId] = l.[RecurringDefinitionId]
    WHERE l.[GoalId] = @GoalId
      AND l.[UserId] = @UserId;
END
GO

-- Two result sets: KPI aggregate (1 row) + top 3 active goals
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Goal_GetDashboard]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        COUNT(CASE WHEN g.[StatusId] = 1 THEN 1 END)  AS [ActiveGoalCount],
        COUNT(CASE WHEN g.[StatusId] = 2 THEN 1 END)  AS [PausedGoalCount],
        COUNT(CASE WHEN g.[StatusId] = 3 THEN 1 END)  AS [CompletedGoalCount],
        ISNULL(SUM(CASE WHEN g.[StatusId] IN (1, 2) THEN g.[TargetAmount]                    ELSE 0 END), 0) AS [TotalTargetAmount],
        ISNULL(SUM(CASE WHEN g.[StatusId] IN (1, 2) THEN g.[CurrentAmount]                   ELSE 0 END), 0) AS [TotalSavedAmount],
        ISNULL(SUM(CASE WHEN g.[StatusId] IN (1, 2) THEN g.[TargetAmount] - g.[CurrentAmount] ELSE 0 END), 0) AS [TotalRemainingAmount]
    FROM [MyMoney].[Goals] g
    WHERE g.[UserId]   = @UserId
      AND g.[StatusId] <> 4;

    SELECT TOP 3
        g.[GoalId],
        g.[Name],
        g.[GoalTypeId],
        g.[TargetAmount],
        g.[CurrentAmount],
        g.[TargetDate],
        g.[Priority],
        g.[StatusId],
        g.[Icon],
        g.[Color],
        CASE WHEN g.[TargetAmount] > 0
             THEN ROUND(CAST(g.[CurrentAmount] AS DECIMAL(18,6)) / CAST(g.[TargetAmount] AS DECIMAL(18,6)) * 100, 2)
             ELSE 0 END AS [CompletionPercent]
    FROM [MyMoney].[Goals] g
    WHERE g.[UserId]   = @UserId
      AND g.[StatusId] = 1
    ORDER BY
        g.[Priority] DESC,
        CASE WHEN g.[TargetDate] IS NOT NULL THEN g.[TargetDate]
             ELSE CAST('9999-12-31' AS DATE) END ASC;
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Goal_GetActiveForScheduleCheck]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        g.[GoalId],
        g.[UserId],
        g.[Name],
        g.[TargetAmount],
        g.[CurrentAmount],
        g.[TargetDate],
        CASE WHEN g.[TargetAmount] > 0
             THEN ROUND(CAST(g.[CurrentAmount] AS DECIMAL(18,6)) / CAST(g.[TargetAmount] AS DECIMAL(18,6)) * 100, 2)
             ELSE 0 END                                                              AS [CompletionPercent],
        DATEDIFF(DAY, CAST(g.[CreatedAtUtc] AS DATE), CAST(GETUTCDATE() AS DATE))   AS [DaysElapsed],
        DATEDIFF(DAY, CAST(g.[CreatedAtUtc] AS DATE), g.[TargetDate])               AS [TotalDays]
    FROM [MyMoney].[Goals] g
    WHERE g.[StatusId]  = 1
      AND g.[TargetDate] IS NOT NULL
      AND g.[TargetDate] > CAST(GETUTCDATE() AS DATE)
      AND (g.[BehindScheduleNotifiedAtUtc] IS NULL
           OR g.[BehindScheduleNotifiedAtUtc] < DATEADD(DAY, -7, GETUTCDATE()));
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Goal_UpdateBehindScheduleNotified]
    @GoalId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[Goals]
    SET    [BehindScheduleNotifiedAtUtc] = GETUTCDATE()
    WHERE  [GoalId] = @GoalId;
END
GO

-- Returns linked transactions generated on @Date that have no GoalContribution yet.
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Goal_GetPendingAutoContributions]
    @Date DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        l.[GoalId],
        l.[UserId],
        t.[TransactionId],
        t.[Amount],
        t.[TransactionDate]
    FROM [MyMoney].[Transactions] t
    INNER JOIN [MyMoney].[GoalRecurringLinks] l
           ON  l.[RecurringDefinitionId] = t.[RecurringDefinitionId]
    INNER JOIN [MyMoney].[Goals] g
           ON  g.[GoalId]   = l.[GoalId]
           AND g.[StatusId] IN (1, 2)
    WHERE t.[TransactionDate]       = @Date
      AND t.[RecurringDefinitionId] IS NOT NULL
      AND NOT EXISTS (
          SELECT 1 FROM [MyMoney].[GoalContributions] gc
          WHERE gc.[LinkedTransactionId] = t.[TransactionId]);
END
GO

-- =============================================================================
-- 3. NOTIFICATION TEMPLATE SEEDS
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = 'Goal_MilestoneReached')
BEGIN
    DECLARE @MilestoneId INT, @CompletedId INT, @BehindId INT;

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('Goal_MilestoneReached', 2, 2, 3);
    SET @MilestoneId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('Goal_Completed', 2, 2, 2);
    SET @CompletedId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('Goal_BehindSchedule', 2, 3, 3);
    SET @BehindId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplateTranslations]
        ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
    VALUES
    (@MilestoneId, 'en', '{MilestonePercent}% Milestone Reached',
                         N'Your goal "{GoalName}" has reached {MilestonePercent}% — you''ve saved {SavedAmount}. Keep going!'),
    (@MilestoneId, 'ar', N'وصلت إلى {MilestonePercent}%',
                         N'هدفك "{GoalName}" وصل إلى {MilestonePercent}% — لقد وفّرت {SavedAmount}. استمر!'),

    (@CompletedId,  'en', 'Goal Completed!',
                         N'Congratulations! Your goal "{GoalName}" is fully funded with {SavedAmount}. Well done!'),
    (@CompletedId,  'ar', N'اكتمل الهدف!',
                         N'تهانينا! هدفك "{GoalName}" اكتمل بمبلغ {SavedAmount}. عمل رائع!'),

    (@BehindId,     'en', 'Goal Behind Schedule',
                         N'Your goal "{GoalName}" is behind schedule. You need {MonthlySavingsNeeded} per month to reach your target by {TargetDate}.'),
    (@BehindId,     'ar', N'الهدف متأخر عن الجدول',
                         N'هدفك "{GoalName}" متأخر عن الجدول. تحتاج إلى {MonthlySavingsNeeded} شهرياً للوصول إلى هدفك بحلول {TargetDate}.');
END
GO
