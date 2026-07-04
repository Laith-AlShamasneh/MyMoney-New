-- =============================================================================
-- Migration 017: Financial Calendar System
-- Date: 2026-06-21
-- Tables:   CalendarEvents, CalendarReminders
-- SPs (14): usp_Calendar_Event_Create
--           usp_Calendar_Event_Update
--           usp_Calendar_Event_Delete
--           usp_Calendar_Event_Get
--           usp_Calendar_Event_Complete
--           usp_Calendar_GetByDay
--           usp_Calendar_GetByWeek
--           usp_Calendar_GetByMonth
--           usp_Calendar_GetAgenda
--           usp_Calendar_GetDashboard
--           usp_Calendar_Search
--           usp_Calendar_Reminder_GetPending
--           usp_Calendar_Reminder_MarkSent
--           usp_Calendar_Reminder_Dismiss
-- Seeds:    2 notification templates (REMINDER_DUE, REMINDER_UPCOMING)
-- =============================================================================

-- =============================================================================
-- 1. TABLES
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CalendarEvents' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[CalendarEvents]
    (
        -- Identity
        [EventId]             BIGINT         IDENTITY(1,1)  NOT NULL,
        [UserId]              BIGINT                        NOT NULL,

        -- Content
        [Title]               NVARCHAR(200)                 NOT NULL,
        [Description]         NVARCHAR(1000)                NULL,

        -- Scheduling
        [EventDate]           DATE                          NOT NULL,
        [StartTime]           TIME(0)                       NULL,
        [EndTime]             TIME(0)                       NULL,
        [AllDay]              BIT                           NOT NULL  CONSTRAINT [DF_CalEv_AllDay]     DEFAULT 1,

        -- Classification
        [EventTypeId]         TINYINT                       NOT NULL  CONSTRAINT [DF_CalEv_TypeId]     DEFAULT 1,
        [Priority]            TINYINT                       NOT NULL  CONSTRAINT [DF_CalEv_Priority]   DEFAULT 2,
        [StatusId]            TINYINT                       NOT NULL  CONSTRAINT [DF_CalEv_StatusId]   DEFAULT 1,

        -- Linked entity (from other modules)
        [LinkedEntityTypeId]  TINYINT                       NULL,
        [LinkedEntityId]      BIGINT                        NULL,

        -- Presentation
        [ColorHex]            NVARCHAR(10)                  NULL,
        [Icon]                NVARCHAR(50)                  NULL,

        -- Reminder
        [NotifyBefore]        INT                           NULL,  -- minutes before event

        -- Metadata
        [MetadataJson]        NVARCHAR(MAX)                 NULL,

        -- Soft delete
        [IsDeleted]           BIT                           NOT NULL  CONSTRAINT [DF_CalEv_IsDeleted]  DEFAULT 0,

        -- Audit
        [CreatedAtUtc]        DATETIME2(0)                  NOT NULL  CONSTRAINT [DF_CalEv_CreatedAt]  DEFAULT GETUTCDATE(),
        [UpdatedAtUtc]        DATETIME2(0)                  NULL,
        [CompletedAtUtc]      DATETIME2(0)                  NULL,

        CONSTRAINT [PK_CalendarEvents]        PRIMARY KEY CLUSTERED ([EventId] ASC),
        CONSTRAINT [FK_CalEv_Users]           FOREIGN KEY ([UserId])  REFERENCES [MyMoney].[Users]([UserId]),
        CONSTRAINT [CK_CalEv_EventTypeId]     CHECK ([EventTypeId]    BETWEEN 1 AND 8),
        CONSTRAINT [CK_CalEv_Priority]        CHECK ([Priority]       BETWEEN 1 AND 4),
        CONSTRAINT [CK_CalEv_StatusId]        CHECK ([StatusId]       BETWEEN 1 AND 3),
        CONSTRAINT [CK_CalEv_LinkedPair]      CHECK
            ([LinkedEntityTypeId] IS NULL AND [LinkedEntityId] IS NULL
          OR [LinkedEntityTypeId] IS NOT NULL AND [LinkedEntityId] IS NOT NULL)
    );

    CREATE NONCLUSTERED INDEX [IX_CalEv_UserId_EventDate]
        ON [MyMoney].[CalendarEvents] ([UserId] ASC, [EventDate] ASC)
        INCLUDE ([EventTypeId], [StatusId], [IsDeleted])
        WHERE ([IsDeleted] = 0);

    CREATE NONCLUSTERED INDEX [IX_CalEv_UserId_Status]
        ON [MyMoney].[CalendarEvents] ([UserId] ASC, [StatusId] ASC)
        WHERE ([IsDeleted] = 0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CalendarReminders' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[CalendarReminders]
    (
        [ReminderId]       BIGINT        IDENTITY(1,1)  NOT NULL,
        [EventId]          BIGINT                       NOT NULL,
        [UserId]           BIGINT                       NOT NULL,
        [ReminderAtUtc]    DATETIME2(0)                 NOT NULL,
        [StatusId]         TINYINT                      NOT NULL  CONSTRAINT [DF_CalRem_StatusId] DEFAULT 1,
        [SentAtUtc]        DATETIME2(0)                 NULL,
        [JobId]            BIGINT                       NULL,
        [CreatedAtUtc]     DATETIME2(0)                 NOT NULL  CONSTRAINT [DF_CalRem_CreatedAt] DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_CalendarReminders]    PRIMARY KEY CLUSTERED ([ReminderId] ASC),
        CONSTRAINT [FK_CalRem_Events]        FOREIGN KEY ([EventId]) REFERENCES [MyMoney].[CalendarEvents]([EventId]) ON DELETE CASCADE,
        CONSTRAINT [FK_CalRem_Users]         FOREIGN KEY ([UserId])  REFERENCES [MyMoney].[Users]([UserId]),
        CONSTRAINT [CK_CalRem_StatusId]      CHECK ([StatusId] BETWEEN 1 AND 3)
    );

    CREATE NONCLUSTERED INDEX [IX_CalRem_Pending]
        ON [MyMoney].[CalendarReminders] ([StatusId] ASC, [ReminderAtUtc] ASC)
        INCLUDE ([EventId], [UserId], [JobId])
        WHERE ([StatusId] = 1);

    CREATE NONCLUSTERED INDEX [IX_CalRem_EventId]
        ON [MyMoney].[CalendarReminders] ([EventId] ASC);
END
GO

-- =============================================================================
-- 2. STORED PROCEDURES
-- =============================================================================

-- ----------------------------------------------------------------------------
-- usp_Calendar_Event_Create
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Event_Create]
    @UserId             BIGINT,
    @Title              NVARCHAR(200),
    @Description        NVARCHAR(1000) = NULL,
    @EventDate          DATE,
    @StartTime          TIME(0)        = NULL,
    @EndTime            TIME(0)        = NULL,
    @AllDay             BIT            = 1,
    @EventTypeId        TINYINT        = 1,
    @Priority           TINYINT        = 2,
    @LinkedEntityTypeId TINYINT        = NULL,
    @LinkedEntityId     BIGINT         = NULL,
    @ColorHex           NVARCHAR(10)   = NULL,
    @Icon               NVARCHAR(50)   = NULL,
    @NotifyBefore       INT            = NULL,
    @MetadataJson       NVARCHAR(MAX)  = NULL,
    @NewEventId         BIGINT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [MyMoney].[CalendarEvents]
        ([UserId], [Title], [Description], [EventDate], [StartTime], [EndTime],
         [AllDay], [EventTypeId], [Priority], [StatusId],
         [LinkedEntityTypeId], [LinkedEntityId],
         [ColorHex], [Icon], [NotifyBefore], [MetadataJson])
    VALUES
        (@UserId, @Title, @Description, @EventDate, @StartTime, @EndTime,
         @AllDay, @EventTypeId, @Priority, 1,
         @LinkedEntityTypeId, @LinkedEntityId,
         @ColorHex, @Icon, @NotifyBefore, @MetadataJson);

    SET @NewEventId = SCOPE_IDENTITY();

    -- Auto-create reminder when NotifyBefore is set and event is in the future
    IF @NotifyBefore IS NOT NULL AND @EventDate >= CAST(GETUTCDATE() AS DATE)
    BEGIN
        DECLARE @ReminderAt DATETIME2(0);
        IF @StartTime IS NOT NULL
            SET @ReminderAt = DATEADD(MINUTE, -@NotifyBefore,
                DATEADD(SECOND,
                    DATEDIFF(SECOND, CAST('00:00:00' AS TIME(0)), @StartTime),
                    CAST(@EventDate AS DATETIME2(0))));
        ELSE
            SET @ReminderAt = DATEADD(MINUTE, -@NotifyBefore,
                CAST(@EventDate AS DATETIME2(0)));

        IF @ReminderAt > GETUTCDATE()
            INSERT INTO [MyMoney].[CalendarReminders]
                ([EventId], [UserId], [ReminderAtUtc], [StatusId])
            VALUES
                (@NewEventId, @UserId, @ReminderAt, 1);
    END
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_Event_Update
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Event_Update]
    @EventId            BIGINT,
    @UserId             BIGINT,
    @Title              NVARCHAR(200),
    @Description        NVARCHAR(1000) = NULL,
    @EventDate          DATE,
    @StartTime          TIME(0)        = NULL,
    @EndTime            TIME(0)        = NULL,
    @AllDay             BIT            = 1,
    @EventTypeId        TINYINT        = 1,
    @Priority           TINYINT        = 2,
    @ColorHex           NVARCHAR(10)   = NULL,
    @Icon               NVARCHAR(50)   = NULL,
    @NotifyBefore       INT            = NULL,
    @AffectedRows       INT            OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[CalendarEvents]
    SET
        [Title]        = @Title,
        [Description]  = @Description,
        [EventDate]    = @EventDate,
        [StartTime]    = @StartTime,
        [EndTime]      = @EndTime,
        [AllDay]       = @AllDay,
        [EventTypeId]  = @EventTypeId,
        [Priority]     = @Priority,
        [ColorHex]     = @ColorHex,
        [Icon]         = @Icon,
        [NotifyBefore] = @NotifyBefore,
        [UpdatedAtUtc] = GETUTCDATE()
    WHERE [EventId]   = @EventId
      AND [UserId]    = @UserId
      AND [IsDeleted] = 0;

    SET @AffectedRows = @@ROWCOUNT;

    IF @AffectedRows > 0
    BEGIN
        -- Refresh reminder: delete old pending ones and re-create if needed
        DELETE FROM [MyMoney].[CalendarReminders]
        WHERE [EventId] = @EventId AND [StatusId] = 1;

        IF @NotifyBefore IS NOT NULL AND @EventDate >= CAST(GETUTCDATE() AS DATE)
        BEGIN
            DECLARE @ReminderAt DATETIME2(0);
            IF @StartTime IS NOT NULL
                SET @ReminderAt = DATEADD(MINUTE, -@NotifyBefore,
                    DATEADD(SECOND,
                        DATEDIFF(SECOND, CAST('00:00:00' AS TIME(0)), @StartTime),
                        CAST(@EventDate AS DATETIME2(0))));
            ELSE
                SET @ReminderAt = DATEADD(MINUTE, -@NotifyBefore,
                    CAST(@EventDate AS DATETIME2(0)));

            IF @ReminderAt > GETUTCDATE()
                INSERT INTO [MyMoney].[CalendarReminders]
                    ([EventId], [UserId], [ReminderAtUtc], [StatusId])
                VALUES
                    (@EventId, @UserId, @ReminderAt, 1);
        END
    END
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_Event_Delete
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Event_Delete]
    @EventId      BIGINT,
    @UserId       BIGINT,
    @AffectedRows INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[CalendarEvents]
    SET [IsDeleted] = 1, [UpdatedAtUtc] = GETUTCDATE()
    WHERE [EventId]   = @EventId
      AND [UserId]    = @UserId
      AND [IsDeleted] = 0;

    SET @AffectedRows = @@ROWCOUNT;
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_Event_Get
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Event_Get]
    @EventId BIGINT,
    @UserId  BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.[EventId],
        e.[Title],
        e.[Description],
        e.[EventDate],
        CAST(e.[StartTime] AS NVARCHAR(8)) AS [StartTime],
        CAST(e.[EndTime]   AS NVARCHAR(8)) AS [EndTime],
        e.[AllDay],
        e.[EventTypeId],
        e.[Priority],
        e.[StatusId],
        e.[LinkedEntityTypeId],
        e.[LinkedEntityId],
        e.[ColorHex],
        e.[Icon],
        e.[NotifyBefore],
        e.[MetadataJson],
        e.[CreatedAtUtc],
        e.[UpdatedAtUtc],
        e.[CompletedAtUtc],
        r.[ReminderId],
        r.[ReminderAtUtc],
        r.[StatusId]  AS [ReminderStatusId]
    FROM [MyMoney].[CalendarEvents] e
    LEFT JOIN [MyMoney].[CalendarReminders] r
        ON r.[EventId] = e.[EventId] AND r.[StatusId] = 1
    WHERE e.[EventId]   = @EventId
      AND e.[UserId]    = @UserId
      AND e.[IsDeleted] = 0;
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_Event_Complete
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Event_Complete]
    @EventId      BIGINT,
    @UserId       BIGINT,
    @AffectedRows INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[CalendarEvents]
    SET
        [StatusId]       = 2,  -- Completed
        [CompletedAtUtc] = GETUTCDATE(),
        [UpdatedAtUtc]   = GETUTCDATE()
    WHERE [EventId]  = @EventId
      AND [UserId]   = @UserId
      AND [StatusId] = 1  -- Only if Pending
      AND [IsDeleted] = 0;

    SET @AffectedRows = @@ROWCOUNT;
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_GetByDay
-- Returns unified events for a single day: user events + module aggregation
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_GetByDay]
    @UserId      BIGINT,
    @Date        DATE,
    @IncludePast BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    -- ── 1. User-defined events ─────────────────────────────────────────────
    SELECT
        e.[EventId],
        CAST(1 AS TINYINT)              AS [SourceId],
        e.[EventTypeId],
        e.[EventDate],
        e.[Title]                        AS [TitleEn],
        e.[Title]                        AS [TitleAr],
        e.[Description],
        e.[Priority],
        e.[StatusId],
        e.[LinkedEntityTypeId],
        e.[LinkedEntityId],
        e.[ColorHex],
        e.[Icon],
        e.[AllDay],
        CAST(e.[StartTime] AS NVARCHAR(8)) AS [StartTime],
        CAST(e.[EndTime]   AS NVARCHAR(8)) AS [EndTime],
        CAST(CASE WHEN e.[StatusId] = 2 THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
        e.[NotifyBefore],
        CAST(NULL AS DECIMAL(18,2))     AS [Amount]
    FROM [MyMoney].[CalendarEvents] e
    WHERE e.[UserId]    = @UserId
      AND e.[EventDate] = @Date
      AND e.[IsDeleted] = 0

    UNION ALL

    -- ── 2. Upcoming recurring transactions ────────────────────────────────
    SELECT
        r.[Id]                          AS [EventId],
        CAST(2 AS TINYINT)              AS [SourceId],
        CAST(CASE WHEN r.[TransactionTypeId] = 1 THEN 2 ELSE 3 END AS TINYINT) AS [EventTypeId],
        r.[NextGenerationDate]           AS [EventDate],
        r.[Name]                         AS [TitleEn],
        r.[Name]                         AS [TitleAr],
        r.[Description],
        CAST(2 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(4 AS TINYINT)              AS [LinkedEntityTypeId],
        r.[Id]                           AS [LinkedEntityId],
        CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
        CAST(NULL AS NVARCHAR(50))      AS [Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(0 AS BIT)                  AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        r.[Amount]
    FROM [MyMoney].[RecurringTransactionDefinitions] r
    WHERE r.[UserId]              = @UserId
      AND r.[StatusId]            = 1
      AND r.[NextGenerationDate]  = @Date

    UNION ALL

    -- ── 3. Goals with target date today ───────────────────────────────────
    SELECT
        g.[GoalId]                      AS [EventId],
        CAST(3 AS TINYINT)              AS [SourceId],
        CAST(4 AS TINYINT)              AS [EventTypeId],
        g.[TargetDate]                   AS [EventDate],
        g.[Name]                         AS [TitleEn],
        g.[Name]                         AS [TitleAr],
        g.[Description],
        CAST(3 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(2 AS TINYINT)              AS [LinkedEntityTypeId],
        g.[GoalId]                       AS [LinkedEntityId],
        g.[Color]                        AS [ColorHex],
        g.[Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(CASE WHEN g.[StatusId] IN (2,3) THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        g.[TargetAmount]                AS [Amount]
    FROM [MyMoney].[Goals] g
    WHERE g.[UserId]     = @UserId
      AND g.[TargetDate] = @Date
      AND g.[StatusId]   NOT IN (4)

    UNION ALL

    -- ── 4. Budget period end dates ─────────────────────────────────────────
    SELECT
        bp.[PeriodId]                   AS [EventId],
        CAST(4 AS TINYINT)              AS [SourceId],
        CAST(5 AS TINYINT)              AS [EventTypeId],
        bp.[PeriodEnd]                   AS [EventDate],
        b.[Name]                         AS [TitleEn],
        b.[Name]                         AS [TitleAr],
        CAST(NULL AS NVARCHAR(1000))    AS [Description],
        CAST(2 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(3 AS TINYINT)              AS [LinkedEntityTypeId],
        b.[BudgetId]                     AS [LinkedEntityId],
        CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
        CAST(NULL AS NVARCHAR(50))      AS [Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(CASE WHEN bp.[StatusId] = 3 THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        bp.[BudgetedAmount]             AS [Amount]
    FROM [MyMoney].[BudgetPeriods] bp
    INNER JOIN [MyMoney].[Budgets] b ON b.[BudgetId] = bp.[BudgetId]
    WHERE bp.[UserId]    = @UserId
      AND bp.[PeriodEnd] = @Date
      AND bp.[StatusId] <> 3

    UNION ALL

    -- ── 5. Subscription renewals ────────────────────────────────────────────
    SELECT
        r.[Id]                          AS [EventId],
        CAST(5 AS TINYINT)              AS [SourceId],
        CAST(6 AS TINYINT)              AS [EventTypeId],
        r.[RenewalDate]                  AS [EventDate],
        d.[Name]                         AS [TitleEn],
        d.[Name]                         AS [TitleAr],
        CAST(NULL AS NVARCHAR(1000))    AS [Description],
        CAST(3 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(5 AS TINYINT)              AS [LinkedEntityTypeId],
        d.[Id]                           AS [LinkedEntityId],
        CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
        CAST(NULL AS NVARCHAR(50))      AS [Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(0 AS BIT)                  AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        d.[Amount]
    FROM [MyMoney].[SubscriptionMetadata] r
    INNER JOIN [MyMoney].[RecurringTransactionDefinitions] d ON d.[Id] = r.[DefinitionId]
    WHERE d.[UserId]     = @UserId
      AND r.[RenewalDate] = @Date
      AND d.[StatusId]   = 1

    ORDER BY [EventDate] ASC, [Priority] DESC;
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_GetByWeek
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_GetByWeek]
    @UserId    BIGINT,
    @WeekStart DATE,
    @WeekEnd   DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.[EventId],
        CAST(1 AS TINYINT)              AS [SourceId],
        e.[EventTypeId],
        e.[EventDate],
        e.[Title]                        AS [TitleEn],
        e.[Title]                        AS [TitleAr],
        e.[Description],
        e.[Priority],
        e.[StatusId],
        e.[LinkedEntityTypeId],
        e.[LinkedEntityId],
        e.[ColorHex],
        e.[Icon],
        e.[AllDay],
        CAST(e.[StartTime] AS NVARCHAR(8)) AS [StartTime],
        CAST(e.[EndTime]   AS NVARCHAR(8)) AS [EndTime],
        CAST(CASE WHEN e.[StatusId] = 2 THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
        e.[NotifyBefore],
        CAST(NULL AS DECIMAL(18,2))     AS [Amount]
    FROM [MyMoney].[CalendarEvents] e
    WHERE e.[UserId]    = @UserId
      AND e.[EventDate] BETWEEN @WeekStart AND @WeekEnd
      AND e.[IsDeleted] = 0

    UNION ALL

    SELECT
        r.[Id]                          AS [EventId],
        CAST(2 AS TINYINT)              AS [SourceId],
        CAST(CASE WHEN r.[TransactionTypeId] = 1 THEN 2 ELSE 3 END AS TINYINT) AS [EventTypeId],
        r.[NextGenerationDate]           AS [EventDate],
        r.[Name]                         AS [TitleEn],
        r.[Name]                         AS [TitleAr],
        r.[Description],
        CAST(2 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(4 AS TINYINT)              AS [LinkedEntityTypeId],
        r.[Id]                           AS [LinkedEntityId],
        CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
        CAST(NULL AS NVARCHAR(50))      AS [Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(0 AS BIT)                  AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        r.[Amount]
    FROM [MyMoney].[RecurringTransactionDefinitions] r
    WHERE r.[UserId]             = @UserId
      AND r.[StatusId]           = 1
      AND r.[NextGenerationDate] BETWEEN @WeekStart AND @WeekEnd

    UNION ALL

    SELECT
        g.[GoalId]                      AS [EventId],
        CAST(3 AS TINYINT)              AS [SourceId],
        CAST(4 AS TINYINT)              AS [EventTypeId],
        g.[TargetDate]                   AS [EventDate],
        g.[Name]                         AS [TitleEn],
        g.[Name]                         AS [TitleAr],
        g.[Description],
        CAST(3 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(2 AS TINYINT)              AS [LinkedEntityTypeId],
        g.[GoalId]                       AS [LinkedEntityId],
        g.[Color]                        AS [ColorHex],
        g.[Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(CASE WHEN g.[StatusId] IN (2,3) THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        g.[TargetAmount]                AS [Amount]
    FROM [MyMoney].[Goals] g
    WHERE g.[UserId]     = @UserId
      AND g.[TargetDate] BETWEEN @WeekStart AND @WeekEnd
      AND g.[StatusId]   NOT IN (4)

    UNION ALL

    SELECT
        bp.[PeriodId]                   AS [EventId],
        CAST(4 AS TINYINT)              AS [SourceId],
        CAST(5 AS TINYINT)              AS [EventTypeId],
        bp.[PeriodEnd]                   AS [EventDate],
        b.[Name]                         AS [TitleEn],
        b.[Name]                         AS [TitleAr],
        CAST(NULL AS NVARCHAR(1000))    AS [Description],
        CAST(2 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(3 AS TINYINT)              AS [LinkedEntityTypeId],
        b.[BudgetId]                     AS [LinkedEntityId],
        CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
        CAST(NULL AS NVARCHAR(50))      AS [Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(CASE WHEN bp.[StatusId] = 3 THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        bp.[BudgetedAmount]             AS [Amount]
    FROM [MyMoney].[BudgetPeriods] bp
    INNER JOIN [MyMoney].[Budgets] b ON b.[BudgetId] = bp.[BudgetId]
    WHERE bp.[UserId]    = @UserId
      AND bp.[PeriodEnd] BETWEEN @WeekStart AND @WeekEnd
      AND bp.[StatusId] <> 3

    UNION ALL

    SELECT
        r.[Id]                          AS [EventId],
        CAST(5 AS TINYINT)              AS [SourceId],
        CAST(6 AS TINYINT)              AS [EventTypeId],
        r.[RenewalDate]                  AS [EventDate],
        d.[Name]                         AS [TitleEn],
        d.[Name]                         AS [TitleAr],
        CAST(NULL AS NVARCHAR(1000))    AS [Description],
        CAST(3 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(5 AS TINYINT)              AS [LinkedEntityTypeId],
        d.[Id]                           AS [LinkedEntityId],
        CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
        CAST(NULL AS NVARCHAR(50))      AS [Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(0 AS BIT)                  AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        d.[Amount]
    FROM [MyMoney].[SubscriptionMetadata] r
    INNER JOIN [MyMoney].[RecurringTransactionDefinitions] d ON d.[Id] = r.[DefinitionId]
    WHERE d.[UserId]      = @UserId
      AND r.[RenewalDate] BETWEEN @WeekStart AND @WeekEnd
      AND d.[StatusId]    = 1

    ORDER BY [EventDate] ASC, [Priority] DESC;
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_GetByMonth
-- Returns all events for a calendar month view
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_GetByMonth]
    @UserId      BIGINT,
    @Year        SMALLINT,
    @Month       TINYINT,
    @EventTypeId TINYINT = NULL  -- optional filter
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MonthStart DATE = DATEFROMPARTS(@Year, @Month, 1);
    DECLARE @MonthEnd   DATE = EOMONTH(@MonthStart);

    SELECT
        e.[EventId],
        CAST(1 AS TINYINT)              AS [SourceId],
        e.[EventTypeId],
        e.[EventDate],
        e.[Title]                        AS [TitleEn],
        e.[Title]                        AS [TitleAr],
        e.[Description],
        e.[Priority],
        e.[StatusId],
        e.[LinkedEntityTypeId],
        e.[LinkedEntityId],
        e.[ColorHex],
        e.[Icon],
        e.[AllDay],
        CAST(e.[StartTime] AS NVARCHAR(8)) AS [StartTime],
        CAST(e.[EndTime]   AS NVARCHAR(8)) AS [EndTime],
        CAST(CASE WHEN e.[StatusId] = 2 THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
        e.[NotifyBefore],
        CAST(NULL AS DECIMAL(18,2))     AS [Amount]
    FROM [MyMoney].[CalendarEvents] e
    WHERE e.[UserId]    = @UserId
      AND e.[EventDate] BETWEEN @MonthStart AND @MonthEnd
      AND e.[IsDeleted] = 0
      AND (@EventTypeId IS NULL OR e.[EventTypeId] = @EventTypeId)

    UNION ALL

    SELECT
        r.[Id]                          AS [EventId],
        CAST(2 AS TINYINT)              AS [SourceId],
        CAST(CASE WHEN r.[TransactionTypeId] = 1 THEN 2 ELSE 3 END AS TINYINT) AS [EventTypeId],
        r.[NextGenerationDate]           AS [EventDate],
        r.[Name]                         AS [TitleEn],
        r.[Name]                         AS [TitleAr],
        r.[Description],
        CAST(2 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(4 AS TINYINT)              AS [LinkedEntityTypeId],
        r.[Id]                           AS [LinkedEntityId],
        CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
        CAST(NULL AS NVARCHAR(50))      AS [Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(0 AS BIT)                  AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        r.[Amount]
    FROM [MyMoney].[RecurringTransactionDefinitions] r
    WHERE r.[UserId]             = @UserId
      AND r.[StatusId]           = 1
      AND r.[NextGenerationDate] BETWEEN @MonthStart AND @MonthEnd
      AND (@EventTypeId IS NULL OR @EventTypeId IN (2, 3))

    UNION ALL

    SELECT
        g.[GoalId]                      AS [EventId],
        CAST(3 AS TINYINT)              AS [SourceId],
        CAST(4 AS TINYINT)              AS [EventTypeId],
        g.[TargetDate]                   AS [EventDate],
        g.[Name]                         AS [TitleEn],
        g.[Name]                         AS [TitleAr],
        g.[Description],
        CAST(3 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(2 AS TINYINT)              AS [LinkedEntityTypeId],
        g.[GoalId]                       AS [LinkedEntityId],
        g.[Color]                        AS [ColorHex],
        g.[Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(CASE WHEN g.[StatusId] IN (2,3) THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        g.[TargetAmount]                AS [Amount]
    FROM [MyMoney].[Goals] g
    WHERE g.[UserId]     = @UserId
      AND g.[TargetDate] BETWEEN @MonthStart AND @MonthEnd
      AND g.[StatusId]   NOT IN (4)
      AND (@EventTypeId IS NULL OR @EventTypeId = 4)

    UNION ALL

    SELECT
        bp.[PeriodId]                   AS [EventId],
        CAST(4 AS TINYINT)              AS [SourceId],
        CAST(5 AS TINYINT)              AS [EventTypeId],
        bp.[PeriodEnd]                   AS [EventDate],
        b.[Name]                         AS [TitleEn],
        b.[Name]                         AS [TitleAr],
        CAST(NULL AS NVARCHAR(1000))    AS [Description],
        CAST(2 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(3 AS TINYINT)              AS [LinkedEntityTypeId],
        b.[BudgetId]                     AS [LinkedEntityId],
        CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
        CAST(NULL AS NVARCHAR(50))      AS [Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(CASE WHEN bp.[StatusId] = 3 THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        bp.[BudgetedAmount]             AS [Amount]
    FROM [MyMoney].[BudgetPeriods] bp
    INNER JOIN [MyMoney].[Budgets] b ON b.[BudgetId] = bp.[BudgetId]
    WHERE bp.[UserId]    = @UserId
      AND bp.[PeriodEnd] BETWEEN @MonthStart AND @MonthEnd
      AND bp.[StatusId] <> 3
      AND (@EventTypeId IS NULL OR @EventTypeId = 5)

    UNION ALL

    SELECT
        sm.[Id]                         AS [EventId],
        CAST(5 AS TINYINT)              AS [SourceId],
        CAST(6 AS TINYINT)              AS [EventTypeId],
        sm.[RenewalDate]                 AS [EventDate],
        d.[Name]                         AS [TitleEn],
        d.[Name]                         AS [TitleAr],
        CAST(NULL AS NVARCHAR(1000))    AS [Description],
        CAST(3 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(5 AS TINYINT)              AS [LinkedEntityTypeId],
        d.[Id]                           AS [LinkedEntityId],
        CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
        CAST(NULL AS NVARCHAR(50))      AS [Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(0 AS BIT)                  AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        d.[Amount]
    FROM [MyMoney].[SubscriptionMetadata] sm
    INNER JOIN [MyMoney].[RecurringTransactionDefinitions] d ON d.[Id] = sm.[DefinitionId]
    WHERE d.[UserId]      = @UserId
      AND sm.[RenewalDate] BETWEEN @MonthStart AND @MonthEnd
      AND d.[StatusId]    = 1
      AND (@EventTypeId IS NULL OR @EventTypeId = 6)

    UNION ALL

    -- Forecast warnings: months with negative projected net
    SELECT
        fp.[PointId]                    AS [EventId],
        CAST(6 AS TINYINT)              AS [SourceId],
        CAST(8 AS TINYINT)              AS [EventTypeId],  -- Custom (forecast warning)
        fp.[MonthYear]                   AS [EventDate],
        CAST(N'Forecast Warning' AS NVARCHAR(200)) AS [TitleEn],
        CAST(N'تحذير توقعات'    AS NVARCHAR(200)) AS [TitleAr],
        CAST(NULL AS NVARCHAR(1000))    AS [Description],
        CAST(3 AS TINYINT)              AS [Priority],
        CAST(1 AS TINYINT)              AS [StatusId],
        CAST(6 AS TINYINT)              AS [LinkedEntityTypeId],
        fp.[ForecastId]                  AS [LinkedEntityId],
        CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
        CAST(NULL AS NVARCHAR(50))      AS [Icon],
        CAST(1 AS BIT)                  AS [AllDay],
        CAST(NULL AS NVARCHAR(8))       AS [StartTime],
        CAST(NULL AS NVARCHAR(8))       AS [EndTime],
        CAST(0 AS BIT)                  AS [IsCompleted],
        CAST(NULL AS INT)               AS [NotifyBefore],
        fp.[ProjectedNet]               AS [Amount]
    FROM [MyMoney].[ForecastMonthlyPoints] fp
    WHERE fp.[UserId]      = @UserId
      AND fp.[MonthYear]   BETWEEN @MonthStart AND @MonthEnd
      AND fp.[ProjectedNet] < 0
      AND (@EventTypeId IS NULL OR @EventTypeId = 8)

    ORDER BY [EventDate] ASC, [Priority] DESC;
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_GetAgenda
-- Returns upcoming events in chronological order (next @DaysAhead days)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_GetAgenda]
    @UserId      BIGINT,
    @StartDate   DATE    = NULL,   -- defaults to today
    @DaysAhead   INT     = 30,
    @PageNumber  INT     = 1,
    @PageSize    INT     = 20
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @From    DATE = ISNULL(@StartDate, CAST(GETUTCDATE() AS DATE));
    DECLARE @To      DATE = DATEADD(DAY, @DaysAhead, @From);
    DECLARE @Offset  INT  = (@PageNumber - 1) * @PageSize;

    WITH CombinedEvents AS
    (
        SELECT
            e.[EventId],
            CAST(1 AS TINYINT)              AS [SourceId],
            e.[EventTypeId],
            e.[EventDate],
            e.[Title]                        AS [TitleEn],
            e.[Title]                        AS [TitleAr],
            e.[Description],
            e.[Priority],
            e.[StatusId],
            e.[LinkedEntityTypeId],
            e.[LinkedEntityId],
            e.[ColorHex],
            e.[Icon],
            e.[AllDay],
            CAST(e.[StartTime] AS NVARCHAR(8)) AS [StartTime],
            CAST(e.[EndTime]   AS NVARCHAR(8)) AS [EndTime],
            CAST(CASE WHEN e.[StatusId] = 2 THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
            e.[NotifyBefore],
            CAST(NULL AS DECIMAL(18,2))     AS [Amount]
        FROM [MyMoney].[CalendarEvents] e
        WHERE e.[UserId]    = @UserId
          AND e.[EventDate] BETWEEN @From AND @To
          AND e.[IsDeleted] = 0
          AND e.[StatusId]  = 1

        UNION ALL

        SELECT
            r.[Id]                          AS [EventId],
            CAST(2 AS TINYINT)              AS [SourceId],
            CAST(CASE WHEN r.[TransactionTypeId] = 1 THEN 2 ELSE 3 END AS TINYINT) AS [EventTypeId],
            r.[NextGenerationDate]           AS [EventDate],
            r.[Name]                         AS [TitleEn],
            r.[Name]                         AS [TitleAr],
            r.[Description],
            CAST(2 AS TINYINT)              AS [Priority],
            CAST(1 AS TINYINT)              AS [StatusId],
            CAST(4 AS TINYINT)              AS [LinkedEntityTypeId],
            r.[Id]                           AS [LinkedEntityId],
            CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
            CAST(NULL AS NVARCHAR(50))      AS [Icon],
            CAST(1 AS BIT)                  AS [AllDay],
            CAST(NULL AS NVARCHAR(8))       AS [StartTime],
            CAST(NULL AS NVARCHAR(8))       AS [EndTime],
            CAST(0 AS BIT)                  AS [IsCompleted],
            CAST(NULL AS INT)               AS [NotifyBefore],
            r.[Amount]
        FROM [MyMoney].[RecurringTransactionDefinitions] r
        WHERE r.[UserId]             = @UserId
          AND r.[StatusId]           = 1
          AND r.[NextGenerationDate] BETWEEN @From AND @To

        UNION ALL

        SELECT
            g.[GoalId]                      AS [EventId],
            CAST(3 AS TINYINT)              AS [SourceId],
            CAST(4 AS TINYINT)              AS [EventTypeId],
            g.[TargetDate]                   AS [EventDate],
            g.[Name]                         AS [TitleEn],
            g.[Name]                         AS [TitleAr],
            g.[Description],
            CAST(3 AS TINYINT)              AS [Priority],
            CAST(1 AS TINYINT)              AS [StatusId],
            CAST(2 AS TINYINT)              AS [LinkedEntityTypeId],
            g.[GoalId]                       AS [LinkedEntityId],
            g.[Color]                        AS [ColorHex],
            g.[Icon],
            CAST(1 AS BIT)                  AS [AllDay],
            CAST(NULL AS NVARCHAR(8))       AS [StartTime],
            CAST(NULL AS NVARCHAR(8))       AS [EndTime],
            CAST(0 AS BIT)                  AS [IsCompleted],
            CAST(NULL AS INT)               AS [NotifyBefore],
            g.[TargetAmount]                AS [Amount]
        FROM [MyMoney].[Goals] g
        WHERE g.[UserId]     = @UserId
          AND g.[TargetDate] BETWEEN @From AND @To
          AND g.[StatusId]   NOT IN (2, 3, 4)

        UNION ALL

        SELECT
            sm.[Id]                         AS [EventId],
            CAST(5 AS TINYINT)              AS [SourceId],
            CAST(6 AS TINYINT)              AS [EventTypeId],
            sm.[RenewalDate]                 AS [EventDate],
            d.[Name]                         AS [TitleEn],
            d.[Name]                         AS [TitleAr],
            CAST(NULL AS NVARCHAR(1000))    AS [Description],
            CAST(3 AS TINYINT)              AS [Priority],
            CAST(1 AS TINYINT)              AS [StatusId],
            CAST(5 AS TINYINT)              AS [LinkedEntityTypeId],
            d.[Id]                           AS [LinkedEntityId],
            CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
            CAST(NULL AS NVARCHAR(50))      AS [Icon],
            CAST(1 AS BIT)                  AS [AllDay],
            CAST(NULL AS NVARCHAR(8))       AS [StartTime],
            CAST(NULL AS NVARCHAR(8))       AS [EndTime],
            CAST(0 AS BIT)                  AS [IsCompleted],
            CAST(NULL AS INT)               AS [NotifyBefore],
            d.[Amount]
        FROM [MyMoney].[SubscriptionMetadata] sm
        INNER JOIN [MyMoney].[RecurringTransactionDefinitions] d ON d.[Id] = sm.[DefinitionId]
        WHERE d.[UserId]      = @UserId
          AND sm.[RenewalDate] BETWEEN @From AND @To
          AND d.[StatusId]    = 1
    )
    SELECT COUNT(*) AS [TotalCount] FROM CombinedEvents;

    WITH CombinedEvents AS
    (
        SELECT
            e.[EventId],
            CAST(1 AS TINYINT)              AS [SourceId],
            e.[EventTypeId],
            e.[EventDate],
            e.[Title]                        AS [TitleEn],
            e.[Title]                        AS [TitleAr],
            e.[Description],
            e.[Priority],
            e.[StatusId],
            e.[LinkedEntityTypeId],
            e.[LinkedEntityId],
            e.[ColorHex],
            e.[Icon],
            e.[AllDay],
            CAST(e.[StartTime] AS NVARCHAR(8)) AS [StartTime],
            CAST(e.[EndTime]   AS NVARCHAR(8)) AS [EndTime],
            CAST(CASE WHEN e.[StatusId] = 2 THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
            e.[NotifyBefore],
            CAST(NULL AS DECIMAL(18,2))     AS [Amount]
        FROM [MyMoney].[CalendarEvents] e
        WHERE e.[UserId]    = @UserId
          AND e.[EventDate] BETWEEN @From AND @To
          AND e.[IsDeleted] = 0
          AND e.[StatusId]  = 1

        UNION ALL

        SELECT
            r.[Id]                          AS [EventId],
            CAST(2 AS TINYINT)              AS [SourceId],
            CAST(CASE WHEN r.[TransactionTypeId] = 1 THEN 2 ELSE 3 END AS TINYINT) AS [EventTypeId],
            r.[NextGenerationDate]           AS [EventDate],
            r.[Name]                         AS [TitleEn],
            r.[Name]                         AS [TitleAr],
            r.[Description],
            CAST(2 AS TINYINT)              AS [Priority],
            CAST(1 AS TINYINT)              AS [StatusId],
            CAST(4 AS TINYINT)              AS [LinkedEntityTypeId],
            r.[Id]                           AS [LinkedEntityId],
            CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
            CAST(NULL AS NVARCHAR(50))      AS [Icon],
            CAST(1 AS BIT)                  AS [AllDay],
            CAST(NULL AS NVARCHAR(8))       AS [StartTime],
            CAST(NULL AS NVARCHAR(8))       AS [EndTime],
            CAST(0 AS BIT)                  AS [IsCompleted],
            CAST(NULL AS INT)               AS [NotifyBefore],
            r.[Amount]
        FROM [MyMoney].[RecurringTransactionDefinitions] r
        WHERE r.[UserId]             = @UserId
          AND r.[StatusId]           = 1
          AND r.[NextGenerationDate] BETWEEN @From AND @To

        UNION ALL

        SELECT
            g.[GoalId]                      AS [EventId],
            CAST(3 AS TINYINT)              AS [SourceId],
            CAST(4 AS TINYINT)              AS [EventTypeId],
            g.[TargetDate]                   AS [EventDate],
            g.[Name]                         AS [TitleEn],
            g.[Name]                         AS [TitleAr],
            g.[Description],
            CAST(3 AS TINYINT)              AS [Priority],
            CAST(1 AS TINYINT)              AS [StatusId],
            CAST(2 AS TINYINT)              AS [LinkedEntityTypeId],
            g.[GoalId]                       AS [LinkedEntityId],
            g.[Color]                        AS [ColorHex],
            g.[Icon],
            CAST(1 AS BIT)                  AS [AllDay],
            CAST(NULL AS NVARCHAR(8))       AS [StartTime],
            CAST(NULL AS NVARCHAR(8))       AS [EndTime],
            CAST(0 AS BIT)                  AS [IsCompleted],
            CAST(NULL AS INT)               AS [NotifyBefore],
            g.[TargetAmount]                AS [Amount]
        FROM [MyMoney].[Goals] g
        WHERE g.[UserId]     = @UserId
          AND g.[TargetDate] BETWEEN @From AND @To
          AND g.[StatusId]   NOT IN (2, 3, 4)

        UNION ALL

        SELECT
            sm.[Id]                         AS [EventId],
            CAST(5 AS TINYINT)              AS [SourceId],
            CAST(6 AS TINYINT)              AS [EventTypeId],
            sm.[RenewalDate]                 AS [EventDate],
            d.[Name]                         AS [TitleEn],
            d.[Name]                         AS [TitleAr],
            CAST(NULL AS NVARCHAR(1000))    AS [Description],
            CAST(3 AS TINYINT)              AS [Priority],
            CAST(1 AS TINYINT)              AS [StatusId],
            CAST(5 AS TINYINT)              AS [LinkedEntityTypeId],
            d.[Id]                           AS [LinkedEntityId],
            CAST(NULL AS NVARCHAR(10))      AS [ColorHex],
            CAST(NULL AS NVARCHAR(50))      AS [Icon],
            CAST(1 AS BIT)                  AS [AllDay],
            CAST(NULL AS NVARCHAR(8))       AS [StartTime],
            CAST(NULL AS NVARCHAR(8))       AS [EndTime],
            CAST(0 AS BIT)                  AS [IsCompleted],
            CAST(NULL AS INT)               AS [NotifyBefore],
            d.[Amount]
        FROM [MyMoney].[SubscriptionMetadata] sm
        INNER JOIN [MyMoney].[RecurringTransactionDefinitions] d ON d.[Id] = sm.[DefinitionId]
        WHERE d.[UserId]      = @UserId
          AND sm.[RenewalDate] BETWEEN @From AND @To
          AND d.[StatusId]    = 1
    )
    SELECT *
    FROM CombinedEvents
    ORDER BY [EventDate] ASC, [Priority] DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_GetDashboard
-- Returns widgets: today, next 7 days summary, upcoming bills, upcoming goals
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_GetDashboard]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today   DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @Next7   DATE = DATEADD(DAY, 7, @Today);
    DECLARE @Next30  DATE = DATEADD(DAY, 30, @Today);

    -- Result Set 1: Today's user-defined events
    SELECT
        e.[EventId],
        CAST(1 AS TINYINT)   AS [SourceId],
        e.[EventTypeId],
        e.[EventDate],
        e.[Title]             AS [TitleEn],
        e.[Title]             AS [TitleAr],
        e.[Priority],
        e.[StatusId],
        e.[LinkedEntityTypeId],
        e.[LinkedEntityId],
        e.[ColorHex],
        e.[Icon],
        CAST(CASE WHEN e.[StatusId] = 2 THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
        CAST(NULL AS DECIMAL(18,2)) AS [Amount]
    FROM [MyMoney].[CalendarEvents] e
    WHERE e.[UserId]    = @UserId
      AND e.[EventDate] = @Today
      AND e.[IsDeleted] = 0
    ORDER BY e.[Priority] DESC;

    -- Result Set 2: Next 7 days - event counts per day
    WITH DateSeries AS (
        SELECT @Today AS [Dt]
        UNION ALL
        SELECT DATEADD(DAY, 1, [Dt]) FROM DateSeries WHERE [Dt] < @Next7
    ),
    EventCounts AS (
        SELECT
            e.[EventDate]  AS [Dt],
            COUNT(*)       AS [EventCount]
        FROM [MyMoney].[CalendarEvents] e
        WHERE e.[UserId]    = @UserId
          AND e.[EventDate] BETWEEN @Today AND @Next7
          AND e.[IsDeleted] = 0
          AND e.[StatusId]  = 1
        GROUP BY e.[EventDate]

        UNION ALL

        SELECT
            r.[NextGenerationDate],
            COUNT(*)
        FROM [MyMoney].[RecurringTransactionDefinitions] r
        WHERE r.[UserId]             = @UserId
          AND r.[StatusId]           = 1
          AND r.[NextGenerationDate] BETWEEN @Today AND @Next7
        GROUP BY r.[NextGenerationDate]
    ),
    DailySummary AS (
        SELECT [Dt], SUM([EventCount]) AS [TotalEvents]
        FROM EventCounts
        GROUP BY [Dt]
    )
    SELECT
        ds.[Dt]                                  AS [EventDate],
        ISNULL(s.[TotalEvents], 0)               AS [EventCount],
        CAST(CASE WHEN ds.[Dt] = @Today THEN 1 ELSE 0 END AS BIT) AS [IsToday]
    FROM DateSeries ds
    LEFT JOIN DailySummary s ON s.[Dt] = ds.[Dt]
    ORDER BY ds.[Dt]
    OPTION (MAXRECURSION 30);

    -- Result Set 3: Upcoming bills (subscriptions in next 30 days)
    SELECT TOP 5
        sm.[Id]              AS [EventId],
        CAST(5 AS TINYINT)   AS [SourceId],
        CAST(6 AS TINYINT)   AS [EventTypeId],
        sm.[RenewalDate]      AS [EventDate],
        d.[Name]              AS [TitleEn],
        d.[Name]              AS [TitleAr],
        CAST(3 AS TINYINT)   AS [Priority],
        CAST(1 AS TINYINT)   AS [StatusId],
        CAST(5 AS TINYINT)   AS [LinkedEntityTypeId],
        d.[Id]                AS [LinkedEntityId],
        CAST(NULL AS NVARCHAR(10)) AS [ColorHex],
        CAST(NULL AS NVARCHAR(50)) AS [Icon],
        CAST(0 AS BIT)       AS [IsCompleted],
        d.[Amount]
    FROM [MyMoney].[SubscriptionMetadata] sm
    INNER JOIN [MyMoney].[RecurringTransactionDefinitions] d ON d.[Id] = sm.[DefinitionId]
    WHERE d.[UserId]      = @UserId
      AND sm.[RenewalDate] BETWEEN @Today AND @Next30
      AND d.[StatusId]    = 1
    ORDER BY sm.[RenewalDate] ASC;

    -- Result Set 4: Upcoming goal deadlines (next 90 days)
    SELECT TOP 5
        g.[GoalId]           AS [EventId],
        CAST(3 AS TINYINT)   AS [SourceId],
        CAST(4 AS TINYINT)   AS [EventTypeId],
        g.[TargetDate]        AS [EventDate],
        g.[Name]              AS [TitleEn],
        g.[Name]              AS [TitleAr],
        CAST(3 AS TINYINT)   AS [Priority],
        CAST(1 AS TINYINT)   AS [StatusId],
        CAST(2 AS TINYINT)   AS [LinkedEntityTypeId],
        g.[GoalId]            AS [LinkedEntityId],
        g.[Color]             AS [ColorHex],
        g.[Icon],
        CAST(0 AS BIT)       AS [IsCompleted],
        g.[TargetAmount]     AS [Amount]
    FROM [MyMoney].[Goals] g
    WHERE g.[UserId]     = @UserId
      AND g.[TargetDate] BETWEEN @Today AND DATEADD(DAY, 90, @Today)
      AND g.[StatusId]   = 1
    ORDER BY g.[TargetDate] ASC;

    -- Result Set 5: Upcoming recurring transactions (next 7 days)
    SELECT TOP 10
        r.[Id]               AS [EventId],
        CAST(2 AS TINYINT)   AS [SourceId],
        CAST(CASE WHEN r.[TransactionTypeId] = 1 THEN 2 ELSE 3 END AS TINYINT) AS [EventTypeId],
        r.[NextGenerationDate] AS [EventDate],
        r.[Name]              AS [TitleEn],
        r.[Name]              AS [TitleAr],
        CAST(2 AS TINYINT)   AS [Priority],
        CAST(1 AS TINYINT)   AS [StatusId],
        CAST(4 AS TINYINT)   AS [LinkedEntityTypeId],
        r.[Id]                AS [LinkedEntityId],
        CAST(NULL AS NVARCHAR(10)) AS [ColorHex],
        CAST(NULL AS NVARCHAR(50)) AS [Icon],
        CAST(0 AS BIT)       AS [IsCompleted],
        r.[Amount]
    FROM [MyMoney].[RecurringTransactionDefinitions] r
    WHERE r.[UserId]             = @UserId
      AND r.[StatusId]           = 1
      AND r.[NextGenerationDate] BETWEEN @Today AND @Next7
    ORDER BY r.[NextGenerationDate] ASC;
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_Search
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Search]
    @UserId      BIGINT,
    @Keyword     NVARCHAR(200)  = NULL,
    @EventTypeId TINYINT        = NULL,
    @SourceId    TINYINT        = NULL,
    @DateFrom    DATE           = NULL,
    @DateTo      DATE           = NULL,
    @StatusId    TINYINT        = NULL,
    @Priority    TINYINT        = NULL,
    @PageNumber  INT            = 1,
    @PageSize    INT            = 20
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    SELECT COUNT(*) AS [TotalCount]
    FROM [MyMoney].[CalendarEvents] e
    WHERE e.[UserId]    = @UserId
      AND e.[IsDeleted] = 0
      AND (@Keyword     IS NULL OR e.[Title] LIKE '%' + @Keyword + '%'
                                OR e.[Description] LIKE '%' + @Keyword + '%')
      AND (@EventTypeId IS NULL OR e.[EventTypeId] = @EventTypeId)
      AND (@DateFrom    IS NULL OR e.[EventDate] >= @DateFrom)
      AND (@DateTo      IS NULL OR e.[EventDate] <= @DateTo)
      AND (@StatusId    IS NULL OR e.[StatusId] = @StatusId)
      AND (@Priority    IS NULL OR e.[Priority] = @Priority)
      AND (@SourceId    IS NULL OR @SourceId = 1);  -- Search only applies to user-defined

    SELECT
        e.[EventId],
        CAST(1 AS TINYINT)              AS [SourceId],
        e.[EventTypeId],
        e.[EventDate],
        e.[Title]                        AS [TitleEn],
        e.[Title]                        AS [TitleAr],
        e.[Description],
        e.[Priority],
        e.[StatusId],
        e.[LinkedEntityTypeId],
        e.[LinkedEntityId],
        e.[ColorHex],
        e.[Icon],
        e.[AllDay],
        CAST(e.[StartTime] AS NVARCHAR(8)) AS [StartTime],
        CAST(e.[EndTime]   AS NVARCHAR(8)) AS [EndTime],
        CAST(CASE WHEN e.[StatusId] = 2 THEN 1 ELSE 0 END AS BIT) AS [IsCompleted],
        e.[NotifyBefore],
        CAST(NULL AS DECIMAL(18,2))     AS [Amount]
    FROM [MyMoney].[CalendarEvents] e
    WHERE e.[UserId]    = @UserId
      AND e.[IsDeleted] = 0
      AND (@Keyword     IS NULL OR e.[Title] LIKE '%' + @Keyword + '%'
                                OR e.[Description] LIKE '%' + @Keyword + '%')
      AND (@EventTypeId IS NULL OR e.[EventTypeId] = @EventTypeId)
      AND (@DateFrom    IS NULL OR e.[EventDate] >= @DateFrom)
      AND (@DateTo      IS NULL OR e.[EventDate] <= @DateTo)
      AND (@StatusId    IS NULL OR e.[StatusId] = @StatusId)
      AND (@Priority    IS NULL OR e.[Priority] = @Priority)
    ORDER BY e.[EventDate] ASC, e.[Priority] DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_Reminder_GetPending
-- Returns reminders due within the next @WindowMinutes minutes
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Reminder_GetPending]
    @WindowMinutes INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Cutoff DATETIME2(0) = DATEADD(MINUTE, @WindowMinutes, GETUTCDATE());

    SELECT
        r.[ReminderId],
        r.[EventId],
        r.[UserId],
        r.[ReminderAtUtc],
        e.[Title],
        e.[EventDate],
        e.[EventTypeId]
    FROM [MyMoney].[CalendarReminders] r
    INNER JOIN [MyMoney].[CalendarEvents] e
        ON e.[EventId] = r.[EventId]
    WHERE r.[StatusId]      = 1          -- Pending
      AND r.[ReminderAtUtc] <= @Cutoff
      AND e.[IsDeleted]     = 0
      AND e.[StatusId]      = 1;         -- Event not yet completed/cancelled
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_Reminder_MarkSent
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Reminder_MarkSent]
    @ReminderId BIGINT,
    @JobId      BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[CalendarReminders]
    SET
        [StatusId]  = 2,  -- Sent
        [SentAtUtc] = GETUTCDATE(),
        [JobId]     = @JobId
    WHERE [ReminderId] = @ReminderId
      AND [StatusId]   = 1;
END
GO

-- ----------------------------------------------------------------------------
-- usp_Calendar_Reminder_Dismiss
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Reminder_Dismiss]
    @ReminderId   BIGINT,
    @UserId       BIGINT,
    @AffectedRows INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[CalendarReminders]
    SET [StatusId] = 3  -- Dismissed
    WHERE [ReminderId] = @ReminderId
      AND [UserId]     = @UserId
      AND [StatusId]   IN (1, 2);

    SET @AffectedRows = @@ROWCOUNT;
END
GO

-- =============================================================================
-- 3. NOTIFICATION TEMPLATES
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = 'CALENDAR_REMINDER_DUE')
BEGIN
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority], [IsActive], [CreatedAtUtc])
    VALUES ('CALENDAR_REMINDER_DUE', 1, 1, 2, 1, GETUTCDATE());

    DECLARE @TemplateId1 INT = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplateTranslations] ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
    VALUES
        (@TemplateId1, 'en', N'Reminder: {EventTitle}',   N'Your event "{EventTitle}" is due on {EventDate}.'),
        (@TemplateId1, 'ar', N'تذكير: {EventTitle}',      N'موعدك "{EventTitle}" هو {EventDate}.');
END

IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = 'CALENDAR_REMINDER_UPCOMING')
BEGIN
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority], [IsActive], [CreatedAtUtc])
    VALUES ('CALENDAR_REMINDER_UPCOMING', 1, 1, 2, 1, GETUTCDATE());

    DECLARE @TemplateId2 INT = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplateTranslations] ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
    VALUES
        (@TemplateId2, 'en', N'Upcoming: {EventTitle}',  N'Reminder: "{EventTitle}" is coming up on {EventDate}.'),
        (@TemplateId2, 'ar', N'قادم: {EventTitle}',      N'تذكير: "{EventTitle}" قادم بتاريخ {EventDate}.');
END

GO
