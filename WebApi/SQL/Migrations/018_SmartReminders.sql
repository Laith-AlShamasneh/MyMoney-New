-- =============================================================================
-- 018_SmartReminders.sql
-- Smart Event Reminder System — extends CalendarReminders with a full delivery
-- lifecycle (Pending → Delivered → Snoozed/Dismissed/Clicked/Expired), a snooze
-- counter, an audit-history table, and the stored procedures that drive the
-- reminder popup + Notification Center integration.
--
-- Reminder status lifecycle (CalendarReminders.StatusId):
--   1 Pending    created, not yet due
--   2 Delivered  due → notification created → popup shows (ACTIVE for popup)
--   3 Dismissed  user closed the popup (stays in Notification Center)
--   4 Snoozed    SnoozedUntilUtc set; re-delivered when due
--   6 Clicked    user opened the event ("Go To Event")
--   7 Expired    auto-expired (event long past)
--
-- Idempotent: guarded column adds, IF NOT EXISTS table, CREATE OR ALTER SPs.
-- =============================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ----------------------------------------------------------------------------
-- 1. CalendarReminders — lifecycle columns
-- ----------------------------------------------------------------------------
IF COL_LENGTH('MyMoney.CalendarReminders', 'SnoozeCount') IS NULL
    ALTER TABLE [MyMoney].[CalendarReminders] ADD [SnoozeCount] INT NOT NULL CONSTRAINT [DF_CalRem_SnoozeCount] DEFAULT (0);
GO
IF COL_LENGTH('MyMoney.CalendarReminders', 'SnoozedUntilUtc') IS NULL
    ALTER TABLE [MyMoney].[CalendarReminders] ADD [SnoozedUntilUtc] DATETIME2(0) NULL;
GO
IF COL_LENGTH('MyMoney.CalendarReminders', 'DeliveredAtUtc') IS NULL
    ALTER TABLE [MyMoney].[CalendarReminders] ADD [DeliveredAtUtc] DATETIME2(0) NULL;
GO
IF COL_LENGTH('MyMoney.CalendarReminders', 'DismissedAtUtc') IS NULL
    ALTER TABLE [MyMoney].[CalendarReminders] ADD [DismissedAtUtc] DATETIME2(0) NULL;
GO
IF COL_LENGTH('MyMoney.CalendarReminders', 'ClickedAtUtc') IS NULL
    ALTER TABLE [MyMoney].[CalendarReminders] ADD [ClickedAtUtc] DATETIME2(0) NULL;
GO
IF COL_LENGTH('MyMoney.CalendarReminders', 'LastActionAtUtc') IS NULL
    ALTER TABLE [MyMoney].[CalendarReminders] ADD [LastActionAtUtc] DATETIME2(0) NULL;
GO

-- Widen the StatusId CHECK constraint to cover the new lifecycle values
-- (1 Pending, 2 Delivered, 3 Dismissed, 4 Snoozed, 6 Clicked, 7 Expired).
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CalRem_StatusId' AND parent_object_id = OBJECT_ID('MyMoney.CalendarReminders'))
    ALTER TABLE [MyMoney].[CalendarReminders] DROP CONSTRAINT [CK_CalRem_StatusId];
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CalRem_StatusId' AND parent_object_id = OBJECT_ID('MyMoney.CalendarReminders'))
    ALTER TABLE [MyMoney].[CalendarReminders]
        ADD CONSTRAINT [CK_CalRem_StatusId] CHECK ([StatusId] IN (1, 2, 3, 4, 6, 7));
GO

-- Active-reminder lookup (per user, by status)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CalendarReminders_User_Status' AND object_id = OBJECT_ID('MyMoney.CalendarReminders'))
    CREATE NONCLUSTERED INDEX [IX_CalendarReminders_User_Status]
        ON [MyMoney].[CalendarReminders] ([UserId] ASC, [StatusId] ASC)
        INCLUDE ([EventId], [ReminderAtUtc], [SnoozeCount]);
GO
-- Snooze re-delivery scan
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CalendarReminders_Status_SnoozedUntil' AND object_id = OBJECT_ID('MyMoney.CalendarReminders'))
    CREATE NONCLUSTERED INDEX [IX_CalendarReminders_Status_SnoozedUntil]
        ON [MyMoney].[CalendarReminders] ([StatusId] ASC, [SnoozedUntilUtc] ASC);
GO

-- ----------------------------------------------------------------------------
-- 2. CalendarReminderHistory — audit trail
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CalendarReminderHistory' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[CalendarReminderHistory]
    (
        [HistoryId]     BIGINT        IDENTITY(1,1) NOT NULL,
        [ReminderId]    BIGINT        NOT NULL,
        [UserId]        BIGINT        NOT NULL,
        [Action]        NVARCHAR(30)  NOT NULL,      -- Delivered/Snoozed/Dismissed/Clicked/Expired
        [FromStatusId]  TINYINT       NULL,
        [ToStatusId]    TINYINT       NULL,
        [DetailJson]    NVARCHAR(400) NULL,
        [CreatedAtUtc]  DATETIME2(0)  NOT NULL CONSTRAINT [DF_CalRemHist_CreatedAt] DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_CalendarReminderHistory] PRIMARY KEY CLUSTERED ([HistoryId] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_CalRemHist_Reminder] ON [MyMoney].[CalendarReminderHistory] ([ReminderId] ASC, [CreatedAtUtc] DESC);
    CREATE NONCLUSTERED INDEX [IX_CalRemHist_User]     ON [MyMoney].[CalendarReminderHistory] ([UserId] ASC, [CreatedAtUtc] DESC);
END
GO

-- ----------------------------------------------------------------------------
-- 3. usp_Calendar_Reminder_GetPending
--    Reminders that should be (re)delivered now: Pending due, OR Snoozed whose
--    snooze window has elapsed. Returns event data + priority for the notification.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Reminder_GetPending]
    @WindowMinutes INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now    DATETIME2(0) = GETUTCDATE();
    DECLARE @Cutoff DATETIME2(0) = DATEADD(MINUTE, @WindowMinutes, @Now);

    SELECT
        r.[ReminderId],
        r.[EventId],
        r.[UserId],
        r.[ReminderAtUtc],
        r.[SnoozeCount],
        e.[Title],
        e.[EventDate],
        e.[EventTypeId],
        e.[Priority]
    FROM [MyMoney].[CalendarReminders] r
    INNER JOIN [MyMoney].[CalendarEvents] e
        ON e.[EventId] = r.[EventId]
    WHERE e.[IsDeleted] = 0
      AND e.[StatusId]  = 1                          -- event still pending
      AND (
            (r.[StatusId] = 1 AND r.[ReminderAtUtc]   <= @Cutoff)   -- Pending, due
         OR (r.[StatusId] = 4 AND r.[SnoozedUntilUtc] <= @Now)      -- Snoozed, elapsed
          );
END
GO

-- ----------------------------------------------------------------------------
-- 4. usp_Calendar_Reminder_MarkSent  (marks Delivered — active for the popup)
--    Idempotent: only transitions Pending(1)/Snoozed(4) → Delivered(2).
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Reminder_MarkSent]
    @ReminderId BIGINT,
    @JobId      BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @From TINYINT = (SELECT [StatusId] FROM [MyMoney].[CalendarReminders] WHERE [ReminderId] = @ReminderId);

    UPDATE [MyMoney].[CalendarReminders]
    SET
        [StatusId]        = 2,               -- Delivered
        [SentAtUtc]       = GETUTCDATE(),
        [DeliveredAtUtc]  = GETUTCDATE(),
        [SnoozedUntilUtc] = NULL,
        [JobId]           = @JobId
    WHERE [ReminderId] = @ReminderId
      AND [StatusId]   IN (1, 4);

    IF @@ROWCOUNT > 0
        INSERT INTO [MyMoney].[CalendarReminderHistory] ([ReminderId], [UserId], [Action], [FromStatusId], [ToStatusId])
        SELECT @ReminderId, [UserId], N'Delivered', @From, 2 FROM [MyMoney].[CalendarReminders] WHERE [ReminderId] = @ReminderId;
END
GO

-- ----------------------------------------------------------------------------
-- 5. usp_Calendar_Reminder_GetActive
--    Active reminders for the popup: Delivered(2), for events still pending.
--    Priority DESC (Critical first), then earliest reminder time.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Reminder_GetActive]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.[ReminderId],
        r.[EventId],
        r.[ReminderAtUtc],
        r.[SnoozeCount],
        e.[Title],
        e.[Description],
        e.[EventDate],
        CAST(e.[StartTime] AS NVARCHAR(8)) AS [StartTime],
        CAST(e.[EndTime]   AS NVARCHAR(8)) AS [EndTime],
        e.[AllDay],
        e.[EventTypeId],
        e.[Priority],
        e.[ColorHex],
        e.[Icon]
    FROM [MyMoney].[CalendarReminders] r
    INNER JOIN [MyMoney].[CalendarEvents] e
        ON e.[EventId] = r.[EventId]
    WHERE r.[UserId]    = @UserId
      AND r.[StatusId]  = 2            -- Delivered
      AND e.[IsDeleted] = 0
      AND e.[StatusId]  = 1
    ORDER BY e.[Priority] DESC, r.[ReminderAtUtc] ASC;
END
GO

-- ----------------------------------------------------------------------------
-- 6. usp_Calendar_Reminder_Snooze
--    @Result:  >=0 new snooze count | -1 not found | -2 limit reached | -3 critical
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Reminder_Snooze]
    @ReminderId   BIGINT,
    @UserId       BIGINT,
    @Minutes      INT,
    @MaxSnoozes   INT,
    @Result       INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Status TINYINT, @Count INT, @Priority TINYINT;

    SELECT @Status = r.[StatusId], @Count = r.[SnoozeCount], @Priority = e.[Priority]
    FROM [MyMoney].[CalendarReminders] r
    INNER JOIN [MyMoney].[CalendarEvents] e ON e.[EventId] = r.[EventId]
    WHERE r.[ReminderId] = @ReminderId AND r.[UserId] = @UserId;

    IF @Status IS NULL              BEGIN SET @Result = -1; RETURN; END
    IF @Priority = 4                BEGIN SET @Result = -3; RETURN; END   -- Critical cannot be snoozed
    IF @Count >= @MaxSnoozes        BEGIN SET @Result = -2; RETURN; END

    UPDATE [MyMoney].[CalendarReminders]
    SET [StatusId]        = 4,       -- Snoozed
        [SnoozeCount]     = [SnoozeCount] + 1,
        [SnoozedUntilUtc] = DATEADD(MINUTE, @Minutes, GETUTCDATE()),
        [LastActionAtUtc] = GETUTCDATE()
    WHERE [ReminderId] = @ReminderId AND [UserId] = @UserId;

    INSERT INTO [MyMoney].[CalendarReminderHistory] ([ReminderId], [UserId], [Action], [FromStatusId], [ToStatusId], [DetailJson])
    VALUES (@ReminderId, @UserId, N'Snoozed', @Status, 4, CONCAT(N'{"minutes":', @Minutes, N',"count":', @Count + 1, N'}'));

    SET @Result = @Count + 1;
END
GO

-- ----------------------------------------------------------------------------
-- 7. usp_Calendar_Reminder_Dismiss
--    @AffectedRows:  >0 dismissed | 0 not found | -3 critical (cannot dismiss)
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Reminder_Dismiss]
    @ReminderId   BIGINT,
    @UserId       BIGINT,
    @AffectedRows INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Status TINYINT, @Priority TINYINT;
    SELECT @Status = r.[StatusId], @Priority = e.[Priority]
    FROM [MyMoney].[CalendarReminders] r
    INNER JOIN [MyMoney].[CalendarEvents] e ON e.[EventId] = r.[EventId]
    WHERE r.[ReminderId] = @ReminderId AND r.[UserId] = @UserId;

    IF @Status IS NULL      BEGIN SET @AffectedRows = 0;  RETURN; END
    IF @Priority = 4        BEGIN SET @AffectedRows = -3; RETURN; END   -- Critical is mandatory

    UPDATE [MyMoney].[CalendarReminders]
    SET [StatusId]        = 3,       -- Dismissed
        [DismissedAtUtc]  = GETUTCDATE(),
        [LastActionAtUtc] = GETUTCDATE()
    WHERE [ReminderId] = @ReminderId
      AND [UserId]     = @UserId
      AND [StatusId]   IN (1, 2, 4);

    SET @AffectedRows = @@ROWCOUNT;

    IF @AffectedRows > 0
        INSERT INTO [MyMoney].[CalendarReminderHistory] ([ReminderId], [UserId], [Action], [FromStatusId], [ToStatusId])
        VALUES (@ReminderId, @UserId, N'Dismissed', @Status, 3);
END
GO

-- ----------------------------------------------------------------------------
-- 8. usp_Calendar_Reminder_MarkClicked  ("Go To Event")
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Reminder_MarkClicked]
    @ReminderId   BIGINT,
    @UserId       BIGINT,
    @AffectedRows INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @From TINYINT = (SELECT [StatusId] FROM [MyMoney].[CalendarReminders] WHERE [ReminderId] = @ReminderId AND [UserId] = @UserId);

    UPDATE [MyMoney].[CalendarReminders]
    SET [StatusId]        = 6,       -- Clicked
        [ClickedAtUtc]    = GETUTCDATE(),
        [LastActionAtUtc] = GETUTCDATE()
    WHERE [ReminderId] = @ReminderId
      AND [UserId]     = @UserId
      AND [StatusId]   IN (1, 2, 4);

    SET @AffectedRows = @@ROWCOUNT;

    IF @AffectedRows > 0
        INSERT INTO [MyMoney].[CalendarReminderHistory] ([ReminderId], [UserId], [Action], [FromStatusId], [ToStatusId])
        VALUES (@ReminderId, @UserId, N'Clicked', @From, 6);
END
GO

-- ----------------------------------------------------------------------------
-- 9. usp_Calendar_Reminder_Expire
--    Auto-expire active/snoozed reminders whose event is well in the past.
--    Runs from the cleanup job; idempotent.
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Reminder_Expire]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Ids TABLE ([ReminderId] BIGINT, [UserId] BIGINT, [FromStatusId] TINYINT);

    UPDATE r
    SET r.[StatusId]        = 7,     -- Expired
        r.[LastActionAtUtc] = GETUTCDATE()
    OUTPUT INSERTED.[ReminderId], INSERTED.[UserId], DELETED.[StatusId] INTO @Ids
    FROM [MyMoney].[CalendarReminders] r
    INNER JOIN [MyMoney].[CalendarEvents] e ON e.[EventId] = r.[EventId]
    WHERE r.[StatusId] IN (1, 2, 4)
      AND (e.[IsDeleted] = 1
        OR e.[StatusId] <> 1
        OR e.[EventDate] < CAST(DATEADD(DAY, -1, GETUTCDATE()) AS DATE));

    INSERT INTO [MyMoney].[CalendarReminderHistory] ([ReminderId], [UserId], [Action], [FromStatusId], [ToStatusId])
    SELECT [ReminderId], [UserId], N'Expired', [FromStatusId], 7 FROM @Ids;

    SELECT COUNT(*) AS [ExpiredCount] FROM @Ids;
END
GO

-- ----------------------------------------------------------------------------
-- 10. usp_Calendar_Reminder_History
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Calendar_Reminder_History]
    @ReminderId BIGINT,
    @UserId     BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        h.[HistoryId],
        h.[ReminderId],
        h.[Action],
        h.[FromStatusId],
        h.[ToStatusId],
        h.[DetailJson],
        h.[CreatedAtUtc]
    FROM [MyMoney].[CalendarReminderHistory] h
    WHERE h.[ReminderId] = @ReminderId
      AND h.[UserId]     = @UserId
    ORDER BY h.[CreatedAtUtc] DESC, h.[HistoryId] DESC;
END
GO
