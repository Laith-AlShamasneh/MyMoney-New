-- =============================================================================
-- Migration: 015_RecurringTransactions_Schema
-- Description: Recurring Transactions & Subscription Engine.
--              - ALTER TABLE: Transactions (add RecurringDefinitionId)
--              - CREATE TABLE: RecurringTransactionDefinitions, SubscriptionMetadata,
--                              RecurringGenerationLog
--              - 4 supporting indexes
--              - Seed: 4 notification templates (EN/AR)
--              - CREATE: 13 stored procedures
-- Author: Laith Al-Shamasneh
-- Date: 2026-06-19
-- =============================================================================


-- =============================================================================
-- 1. RecurringTransactionDefinitions
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RecurringTransactionDefinitions' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[RecurringTransactionDefinitions]
    (
        [Id]                  BIGINT         NOT NULL IDENTITY(1, 1),
        [UserId]              BIGINT         NOT NULL,
        [CategoryId]          INT            NOT NULL,
        [TransactionTypeId]   TINYINT        NOT NULL,   -- 1=Income, 2=Expense
        [Name]                NVARCHAR(200)  NOT NULL,
        [Amount]              DECIMAL(18, 2) NOT NULL,
        [Description]         NVARCHAR(500)  NULL,
        [FrequencyId]         TINYINT        NOT NULL,   -- 1=Daily, 2=Weekly, 3=Monthly, 4=Quarterly, 5=Yearly, 6=Custom
        [FrequencyInterval]   INT            NULL,       -- Custom only: e.g. every 2 (weeks)
        [FrequencyUnit]       TINYINT        NULL,       -- Custom only: 1=Days, 2=Weeks, 3=Months, 4=Years
        [DayOfMonth]          TINYINT        NULL,       -- 0=last day, 1-28 for monthly/quarterly/yearly
        [DayOfWeek]           TINYINT        NULL,       -- 0=Sunday .. 6=Saturday for weekly
        [StartDate]           DATE           NOT NULL,
        [EndDate]             DATE           NULL,
        [IsSubscription]      BIT            NOT NULL CONSTRAINT [DF_RTD_IsSubscription]  DEFAULT 0,
        [StatusId]            TINYINT        NOT NULL CONSTRAINT [DF_RTD_StatusId]        DEFAULT 1,  -- 1=Active
        [LastGeneratedDate]   DATE           NULL,
        [NextGenerationDate]  DATE           NULL,
        [Notes]               NVARCHAR(1000) NULL,
        [CreatedAtUtc]        DATETIME2(0)   NOT NULL CONSTRAINT [DF_RTD_CreatedAtUtc]   DEFAULT GETUTCDATE(),
        [UpdatedAtUtc]        DATETIME2(0)   NULL,

        CONSTRAINT [PK_RecurringTransactionDefinitions]     PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_RTD_Users]      FOREIGN KEY ([UserId])     REFERENCES [MyMoney].[Users]([UserId]) ON DELETE CASCADE,
        CONSTRAINT [FK_RTD_Categories] FOREIGN KEY ([CategoryId]) REFERENCES [MyMoney].[Categories]([CategoryId]),
        CONSTRAINT [CK_RTD_TransactionTypeId] CHECK ([TransactionTypeId] IN (1, 2)),
        CONSTRAINT [CK_RTD_FrequencyId]       CHECK ([FrequencyId] BETWEEN 1 AND 6),
        CONSTRAINT [CK_RTD_StatusId]          CHECK ([StatusId]    BETWEEN 1 AND 4),
        CONSTRAINT [CK_RTD_DayOfMonth]        CHECK ([DayOfMonth] IS NULL OR [DayOfMonth] BETWEEN 0 AND 28),
        CONSTRAINT [CK_RTD_DayOfWeek]         CHECK ([DayOfWeek]  IS NULL OR [DayOfWeek]  BETWEEN 0 AND 6)
    );

    CREATE NONCLUSTERED INDEX [IX_RTD_UserId_Status]
        ON [MyMoney].[RecurringTransactionDefinitions] ([UserId], [StatusId])
        INCLUDE ([Name], [Amount], [TransactionTypeId], [FrequencyId], [IsSubscription], [NextGenerationDate]);

    CREATE NONCLUSTERED INDEX [IX_RTD_Status_NextGenDate]
        ON [MyMoney].[RecurringTransactionDefinitions] ([StatusId], [NextGenerationDate])
        WHERE [StatusId] = 1;
END
GO

-- =============================================================================
-- 2. SubscriptionMetadata
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SubscriptionMetadata' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[SubscriptionMetadata]
    (
        [Id]           INT           NOT NULL IDENTITY(1, 1),
        [DefinitionId] BIGINT        NOT NULL,
        [ProviderName] NVARCHAR(200) NOT NULL,
        [WebsiteUrl]   NVARCHAR(500) NULL,
        [AutoRenew]    BIT           NOT NULL CONSTRAINT [DF_SM_AutoRenew] DEFAULT 1,
        [RenewalDate]  DATE          NULL,
        [TrialEndDate] DATE          NULL,

        CONSTRAINT [PK_SubscriptionMetadata]            PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_SubscriptionMetadata_Definition] UNIQUE ([DefinitionId]),
        CONSTRAINT [FK_SM_Definition]
            FOREIGN KEY ([DefinitionId]) REFERENCES [MyMoney].[RecurringTransactionDefinitions]([Id]) ON DELETE CASCADE
    );
END
GO

-- =============================================================================
-- 3. RecurringGenerationLog
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RecurringGenerationLog' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[RecurringGenerationLog]
    (
        [Id]               BIGINT       NOT NULL IDENTITY(1, 1),
        [DefinitionId]     BIGINT       NOT NULL,
        [GeneratedForDate] DATE         NOT NULL,
        [TransactionId]    BIGINT       NOT NULL,
        [GeneratedAtUtc]   DATETIME2(0) NOT NULL CONSTRAINT [DF_RGL_GeneratedAtUtc] DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_RecurringGenerationLog]            PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_RGL_DefinitionId_ForDate]          UNIQUE ([DefinitionId], [GeneratedForDate]),
        CONSTRAINT [FK_RGL_Definition]
            FOREIGN KEY ([DefinitionId]) REFERENCES [MyMoney].[RecurringTransactionDefinitions]([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_RGL_DefinitionId_ForDate]
        ON [MyMoney].[RecurringGenerationLog] ([DefinitionId], [GeneratedForDate] DESC);

    CREATE NONCLUSTERED INDEX [IX_RGL_TransactionId]
        ON [MyMoney].[RecurringGenerationLog] ([TransactionId]);
END
GO

-- =============================================================================
-- 4. Transactions — add RecurringDefinitionId (nullable back-reference)
-- =============================================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[MyMoney].[Transactions]') AND name = 'RecurringDefinitionId')
BEGIN
    ALTER TABLE [MyMoney].[Transactions]
        ADD [RecurringDefinitionId] BIGINT NULL;

    ALTER TABLE [MyMoney].[Transactions]
        ADD CONSTRAINT [FK_Transactions_RecurringDefinition]
            FOREIGN KEY ([RecurringDefinitionId])
            REFERENCES [MyMoney].[RecurringTransactionDefinitions]([Id])
            ON DELETE SET NULL;
END
GO

-- =============================================================================
-- 5. Notification template seeds
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = 'RecurringPayment.Generated')
BEGIN
    DECLARE @RpGeneratedId    INT, @RsAutoRenewedId INT,
            @RpUpcomingId     INT, @RsUpcomingId    INT;

    -- Recurring / Financial (Category=2)
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('RecurringPayment.Generated', 2, 2, 4); SET @RpGeneratedId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('RecurringSubscription.AutoRenewed', 2, 2, 4); SET @RsAutoRenewedId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('RecurringPayment.UpcomingDue', 2, 3, 3); SET @RpUpcomingId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('RecurringSubscription.UpcomingRenewal', 2, 3, 3); SET @RsUpcomingId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplateTranslations]
        ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
    VALUES
    -- RecurringPayment.Generated
    (@RpGeneratedId,    'en', 'Recurring Payment Processed',
                              'Your recurring payment "{Name}" of {Amount} was automatically recorded for {Date}.'),
    (@RpGeneratedId,    'ar', N'تمت معالجة الدفعة المتكررة',
                              N'تم تسجيل دفعتك المتكررة "{Name}" بقيمة {Amount} تلقائياً بتاريخ {Date}.'),

    -- RecurringSubscription.AutoRenewed
    (@RsAutoRenewedId,  'en', 'Subscription Auto-Renewed',
                              'Your subscription to {ProviderName} of {Amount} has been automatically renewed.'),
    (@RsAutoRenewedId,  'ar', N'تم تجديد الاشتراك تلقائياً',
                              N'تم تجديد اشتراكك في {ProviderName} بقيمة {Amount} تلقائياً.'),

    -- RecurringPayment.UpcomingDue
    (@RpUpcomingId,     'en', 'Upcoming Payment Reminder',
                              'Your recurring payment "{Name}" of {Amount} is due in {DaysUntil} day(s) on {DueDate}.'),
    (@RpUpcomingId,     'ar', N'تذكير بدفعة قادمة',
                              N'دفعتك المتكررة "{Name}" بقيمة {Amount} مستحقة خلال {DaysUntil} يوم(أيام) بتاريخ {DueDate}.'),

    -- RecurringSubscription.UpcomingRenewal
    (@RsUpcomingId,     'en', 'Subscription Renewal Reminder',
                              'Your {ProviderName} subscription of {Amount} renews in {DaysUntil} day(s) on {RenewalDate}.'),
    (@RsUpcomingId,     'ar', N'تذكير بتجديد الاشتراك',
                              N'اشتراكك في {ProviderName} بقيمة {Amount} سيتجدد خلال {DaysUntil} يوم(أيام) بتاريخ {RenewalDate}.');
END
GO


-- =============================================================================
-- STORED PROCEDURES
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. usp_RecurringTransaction_Create
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_Create', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_Create];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_Create]
    @UserId             BIGINT,
    @CategoryId         INT,
    @TransactionTypeId  TINYINT,
    @Name               NVARCHAR(200),
    @Amount             DECIMAL(18, 2),
    @Description        NVARCHAR(500),
    @FrequencyId        TINYINT,
    @FrequencyInterval  INT,
    @FrequencyUnit      TINYINT,
    @DayOfMonth         TINYINT,
    @DayOfWeek          TINYINT,
    @StartDate          DATE,
    @EndDate            DATE,
    @IsSubscription     BIT,
    @Notes              NVARCHAR(1000),
    @NextGenerationDate DATE,
    -- Subscription-specific (NULL for non-subscriptions)
    @ProviderName       NVARCHAR(200),
    @WebsiteUrl         NVARCHAR(500),
    @AutoRenew          BIT,
    @RenewalDate        DATE,
    @TrialEndDate       DATE,
    @NewId              BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [MyMoney].[RecurringTransactionDefinitions]
        ([UserId], [CategoryId], [TransactionTypeId], [Name], [Amount], [Description],
         [FrequencyId], [FrequencyInterval], [FrequencyUnit], [DayOfMonth], [DayOfWeek],
         [StartDate], [EndDate], [IsSubscription], [Notes], [NextGenerationDate])
    VALUES
        (@UserId, @CategoryId, @TransactionTypeId, @Name, @Amount, @Description,
         @FrequencyId, @FrequencyInterval, @FrequencyUnit, @DayOfMonth, @DayOfWeek,
         @StartDate, @EndDate, @IsSubscription, @Notes, @NextGenerationDate);

    SET @NewId = SCOPE_IDENTITY();

    IF @IsSubscription = 1 AND @ProviderName IS NOT NULL
    BEGIN
        INSERT INTO [MyMoney].[SubscriptionMetadata]
            ([DefinitionId], [ProviderName], [WebsiteUrl], [AutoRenew], [RenewalDate], [TrialEndDate])
        VALUES
            (@NewId, @ProviderName, @WebsiteUrl, ISNULL(@AutoRenew, 1), @RenewalDate, @TrialEndDate);
    END
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. usp_RecurringTransaction_GetById
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_GetById', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_GetById];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_GetById]
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        rtd.[Id],
        rtd.[UserId],
        rtd.[CategoryId],
        c.[NameEn]            AS [CategoryNameEn],
        c.[NameAr]            AS [CategoryNameAr],
        rtd.[TransactionTypeId],
        rtd.[Name],
        rtd.[Amount],
        rtd.[Description],
        rtd.[FrequencyId],
        rtd.[FrequencyInterval],
        rtd.[FrequencyUnit],
        rtd.[DayOfMonth],
        rtd.[DayOfWeek],
        rtd.[StartDate],
        rtd.[EndDate],
        rtd.[StatusId],
        rtd.[IsSubscription],
        rtd.[LastGeneratedDate],
        rtd.[NextGenerationDate],
        rtd.[Notes],
        rtd.[CreatedAtUtc]    AS [CreatedAt],
        rtd.[UpdatedAtUtc]    AS [UpdatedAt],
        -- Subscription metadata (NULL for non-subscriptions)
        sm.[ProviderName],
        sm.[WebsiteUrl],
        sm.[AutoRenew],
        sm.[RenewalDate],
        sm.[TrialEndDate]
    FROM [MyMoney].[RecurringTransactionDefinitions] rtd
    INNER JOIN [MyMoney].[Categories] c ON c.[CategoryId] = rtd.[CategoryId]
    LEFT  JOIN [MyMoney].[SubscriptionMetadata]       sm ON sm.[DefinitionId] = rtd.[Id]
    WHERE rtd.[Id] = @Id;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. usp_RecurringTransaction_GetList
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_GetList', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_GetList];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_GetList]
    @UserId            BIGINT,
    @StatusId          TINYINT,
    @TransactionTypeId TINYINT,
    @IsSubscription    BIT,
    @PageNumber        INT,
    @PageSize          INT,
    @TotalCount        INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    SELECT @TotalCount = COUNT(*)
    FROM [MyMoney].[RecurringTransactionDefinitions] rtd
    WHERE rtd.[UserId] = @UserId
      AND (@StatusId          IS NULL OR rtd.[StatusId]          = @StatusId)
      AND (@TransactionTypeId IS NULL OR rtd.[TransactionTypeId] = @TransactionTypeId)
      AND (@IsSubscription    IS NULL OR rtd.[IsSubscription]    = @IsSubscription);

    SELECT
        rtd.[Id],
        rtd.[UserId],
        rtd.[CategoryId],
        c.[NameEn]            AS [CategoryNameEn],
        c.[NameAr]            AS [CategoryNameAr],
        rtd.[TransactionTypeId],
        rtd.[Name],
        rtd.[Amount],
        rtd.[Description],
        rtd.[FrequencyId],
        rtd.[FrequencyInterval],
        rtd.[FrequencyUnit],
        rtd.[DayOfMonth],
        rtd.[DayOfWeek],
        rtd.[StartDate],
        rtd.[EndDate],
        rtd.[StatusId],
        rtd.[IsSubscription],
        rtd.[LastGeneratedDate],
        rtd.[NextGenerationDate],
        rtd.[Notes],
        rtd.[CreatedAtUtc]    AS [CreatedAt],
        rtd.[UpdatedAtUtc]    AS [UpdatedAt],
        sm.[ProviderName],
        sm.[WebsiteUrl],
        sm.[AutoRenew],
        sm.[RenewalDate],
        sm.[TrialEndDate]
    FROM [MyMoney].[RecurringTransactionDefinitions] rtd
    INNER JOIN [MyMoney].[Categories] c ON c.[CategoryId] = rtd.[CategoryId]
    LEFT  JOIN [MyMoney].[SubscriptionMetadata]       sm ON sm.[DefinitionId] = rtd.[Id]
    WHERE rtd.[UserId] = @UserId
      AND (@StatusId          IS NULL OR rtd.[StatusId]          = @StatusId)
      AND (@TransactionTypeId IS NULL OR rtd.[TransactionTypeId] = @TransactionTypeId)
      AND (@IsSubscription    IS NULL OR rtd.[IsSubscription]    = @IsSubscription)
    ORDER BY rtd.[NextGenerationDate] ASC, rtd.[Id] ASC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 4. usp_RecurringTransaction_Update
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_Update', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_Update];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_Update]
    @Id                 BIGINT,
    @UserId             BIGINT,
    @CategoryId         INT,
    @Name               NVARCHAR(200),
    @Amount             DECIMAL(18, 2),
    @Description        NVARCHAR(500),
    @FrequencyInterval  INT,
    @FrequencyUnit      TINYINT,
    @DayOfMonth         TINYINT,
    @DayOfWeek          TINYINT,
    @EndDate            DATE,
    @Notes              NVARCHAR(1000),
    @NextGenerationDate DATE,
    -- Subscription-specific
    @ProviderName       NVARCHAR(200),
    @WebsiteUrl         NVARCHAR(500),
    @AutoRenew          BIT,
    @RenewalDate        DATE,
    @TrialEndDate       DATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[RecurringTransactionDefinitions]
    SET
        [CategoryId]        = @CategoryId,
        [Name]              = @Name,
        [Amount]            = @Amount,
        [Description]       = @Description,
        [FrequencyInterval] = @FrequencyInterval,
        [FrequencyUnit]     = @FrequencyUnit,
        [DayOfMonth]        = @DayOfMonth,
        [DayOfWeek]         = @DayOfWeek,
        [EndDate]           = @EndDate,
        [Notes]             = @Notes,
        [NextGenerationDate]= @NextGenerationDate,
        [UpdatedAtUtc]      = GETUTCDATE()
    WHERE [Id] = @Id AND [UserId] = @UserId;

    -- Upsert subscription metadata
    IF @ProviderName IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM [MyMoney].[SubscriptionMetadata] WHERE [DefinitionId] = @Id)
        BEGIN
            UPDATE [MyMoney].[SubscriptionMetadata]
            SET [ProviderName] = @ProviderName,
                [WebsiteUrl]   = @WebsiteUrl,
                [AutoRenew]    = ISNULL(@AutoRenew, 1),
                [RenewalDate]  = @RenewalDate,
                [TrialEndDate] = @TrialEndDate
            WHERE [DefinitionId] = @Id;
        END
        ELSE
        BEGIN
            INSERT INTO [MyMoney].[SubscriptionMetadata]
                ([DefinitionId], [ProviderName], [WebsiteUrl], [AutoRenew], [RenewalDate], [TrialEndDate])
            VALUES
                (@Id, @ProviderName, @WebsiteUrl, ISNULL(@AutoRenew, 1), @RenewalDate, @TrialEndDate);
        END
    END
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 5. usp_RecurringTransaction_Delete
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_Delete', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_Delete];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_Delete]
    @Id     BIGINT,
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- Orphan generated transactions (preserve history; FK SET NULL handles this)
    DELETE FROM [MyMoney].[RecurringTransactionDefinitions]
    WHERE [Id] = @Id AND [UserId] = @UserId;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 6. usp_RecurringTransaction_Pause
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_Pause', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_Pause];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_Pause]
    @Id      BIGINT,
    @UserId  BIGINT,
    @Success BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[RecurringTransactionDefinitions]
    SET [StatusId]     = 2,          -- Paused
        [UpdatedAtUtc] = GETUTCDATE()
    WHERE [Id] = @Id AND [UserId] = @UserId
      AND [StatusId] = 1;            -- Only if currently Active

    SET @Success = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 7. usp_RecurringTransaction_Resume
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_Resume', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_Resume];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_Resume]
    @Id      BIGINT,
    @UserId  BIGINT,
    @Success BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [MyMoney].[RecurringTransactionDefinitions]
    SET [StatusId]     = 1,          -- Active
        [UpdatedAtUtc] = GETUTCDATE()
    WHERE [Id] = @Id AND [UserId] = @UserId
      AND [StatusId] = 2;            -- Only if currently Paused

    SET @Success = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 8. usp_RecurringTransaction_GetDue
--    Returns all Active definitions whose NextGenerationDate <= @UpToDate
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_GetDue', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_GetDue];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_GetDue]
    @UpToDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        rtd.[Id],
        rtd.[UserId],
        rtd.[CategoryId],
        rtd.[TransactionTypeId],
        rtd.[Name],
        rtd.[Amount],
        rtd.[Description],
        rtd.[FrequencyId],
        rtd.[FrequencyInterval],
        rtd.[FrequencyUnit],
        rtd.[DayOfMonth],
        rtd.[DayOfWeek],
        rtd.[StartDate],
        rtd.[NextGenerationDate],
        rtd.[LastGeneratedDate],
        rtd.[EndDate],
        rtd.[IsSubscription],
        sm.[ProviderName]
    FROM [MyMoney].[RecurringTransactionDefinitions] rtd
    LEFT JOIN [MyMoney].[SubscriptionMetadata] sm ON sm.[DefinitionId] = rtd.[Id]
    WHERE rtd.[StatusId]         = 1                 -- Active
      AND rtd.[NextGenerationDate] <= @UpToDate
      AND rtd.[StartDate]          <= @UpToDate
      AND (rtd.[EndDate] IS NULL OR rtd.[EndDate] >= rtd.[NextGenerationDate]);
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 9. usp_RecurringTransaction_GenerateNext
--    Idempotent: inserts a Transaction, logs it, advances NextGenerationDate.
--    Concurrent duplicates handled via TRY/CATCH on the UNIQUE log constraint.
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_GenerateNext', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_GenerateNext];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_GenerateNext]
    @DefinitionId       BIGINT,
    @ForDate            DATE,
    @NextGenerationDate DATE,
    @TransactionId      BIGINT  OUTPUT,
    @WasAlreadyDone     BIT     OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Fast-path: already generated for this date
    SELECT @TransactionId = [TransactionId]
    FROM   [MyMoney].[RecurringGenerationLog]
    WHERE  [DefinitionId]     = @DefinitionId
      AND  [GeneratedForDate] = @ForDate;

    IF @TransactionId IS NOT NULL
    BEGIN
        SET @WasAlreadyDone = 1;
        RETURN;
    END

    -- Load definition fields needed for Transaction row
    DECLARE @UserId            BIGINT,
            @CategoryId        INT,
            @TransTypeId       TINYINT,
            @Name              NVARCHAR(200),
            @Amount            DECIMAL(18, 2),
            @Description       NVARCHAR(500),
            @Notes             NVARCHAR(1000),
            @EndDate           DATE;

    SELECT
        @UserId      = [UserId],
        @CategoryId  = [CategoryId],
        @TransTypeId = [TransactionTypeId],
        @Name        = [Name],
        @Amount      = [Amount],
        @Description = [Description],
        @Notes       = [Notes],
        @EndDate     = [EndDate]
    FROM [MyMoney].[RecurringTransactionDefinitions]
    WHERE [Id] = @DefinitionId;

    BEGIN TRY
        -- Insert the generated transaction
        INSERT INTO [MyMoney].[Transactions]
            ([UserId], [CategoryId], [TransactionTypeId], [Amount], [Description],
             [TransactionDate], [Notes], [IsActive], [CreatedAt], [RecurringDefinitionId])
        VALUES
            (@UserId, @CategoryId, @TransTypeId, @Amount, @Description,
             @ForDate, @Notes, 1, GETUTCDATE(), @DefinitionId);

        DECLARE @NewTransId BIGINT = SCOPE_IDENTITY();

        -- Log the generation (UNIQUE constraint = idempotency guard)
        INSERT INTO [MyMoney].[RecurringGenerationLog]
            ([DefinitionId], [GeneratedForDate], [TransactionId])
        VALUES
            (@DefinitionId, @ForDate, @NewTransId);

        -- Advance the definition
        UPDATE [MyMoney].[RecurringTransactionDefinitions]
        SET
            [LastGeneratedDate]  = @ForDate,
            [NextGenerationDate] = @NextGenerationDate,
            -- Auto-expire when the definition's end date is reached
            [StatusId]           = CASE
                                       WHEN @EndDate IS NOT NULL AND @ForDate >= @EndDate THEN 4
                                       ELSE [StatusId]
                                   END,
            [UpdatedAtUtc]       = GETUTCDATE()
        WHERE [Id] = @DefinitionId;

        SET @TransactionId  = @NewTransId;
        SET @WasAlreadyDone = 0;
    END TRY
    BEGIN CATCH
        -- Re-check after duplicate key violation (concurrent processor race)
        SELECT @TransactionId = [TransactionId]
        FROM   [MyMoney].[RecurringGenerationLog]
        WHERE  [DefinitionId]     = @DefinitionId
          AND  [GeneratedForDate] = @ForDate;

        IF @TransactionId IS NOT NULL
            SET @WasAlreadyDone = 1;
        ELSE
            THROW;
    END CATCH
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 10. usp_RecurringTransaction_GetUpcoming
--     All active definitions due within the next @DaysAhead days (system-wide,
--     used by the notification scheduler to fan out per-user alerts).
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_GetUpcoming', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_GetUpcoming];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_GetUpcoming]
    @DaysAhead INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today    DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @Deadline DATE = DATEADD(DAY, @DaysAhead, @Today);

    SELECT
        rtd.[Id],
        rtd.[UserId],
        rtd.[Name],
        rtd.[Amount],
        rtd.[NextGenerationDate]           AS [NextDate],
        DATEDIFF(DAY, @Today, rtd.[NextGenerationDate]) AS [DaysUntil],
        c.[NameEn]                         AS [CategoryNameEn],
        c.[NameAr]                         AS [CategoryNameAr],
        rtd.[IsSubscription],
        sm.[ProviderName]
    FROM [MyMoney].[RecurringTransactionDefinitions] rtd
    INNER JOIN [MyMoney].[Categories] c ON c.[CategoryId] = rtd.[CategoryId]
    LEFT  JOIN [MyMoney].[SubscriptionMetadata] sm ON sm.[DefinitionId] = rtd.[Id]
    WHERE rtd.[StatusId] = 1
      AND rtd.[NextGenerationDate] >= @Today
      AND rtd.[NextGenerationDate] <= @Deadline
    ORDER BY rtd.[NextGenerationDate] ASC;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 11. usp_RecurringTransaction_GetDashboardSummary
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_GetDashboardSummary', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_GetDashboardSummary];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_GetDashboardSummary]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ISNULL(SUM(CASE WHEN rtd.[TransactionTypeId] = 1 THEN
            CASE rtd.[FrequencyId]
                WHEN 1 THEN rtd.[Amount] * 30.0    -- Daily  → monthly
                WHEN 2 THEN rtd.[Amount] * 4.0     -- Weekly → monthly
                WHEN 3 THEN rtd.[Amount]            -- Monthly
                WHEN 4 THEN rtd.[Amount] / 3.0      -- Quarterly → monthly
                WHEN 5 THEN rtd.[Amount] / 12.0     -- Yearly → monthly
                ELSE         rtd.[Amount]            -- Custom → raw amount
            END ELSE 0 END), 0)                             AS [MonthlyRecurringIncome],

        ISNULL(SUM(CASE WHEN rtd.[TransactionTypeId] = 2 THEN
            CASE rtd.[FrequencyId]
                WHEN 1 THEN rtd.[Amount] * 30.0
                WHEN 2 THEN rtd.[Amount] * 4.0
                WHEN 3 THEN rtd.[Amount]
                WHEN 4 THEN rtd.[Amount] / 3.0
                WHEN 5 THEN rtd.[Amount] / 12.0
                ELSE         rtd.[Amount]
            END ELSE 0 END), 0)                             AS [MonthlyRecurringExpenses],

        COUNT(*)                                            AS [ActiveDefinitionsCount],
        SUM(CAST(rtd.[IsSubscription] AS INT))              AS [ActiveSubscriptionsCount]

    FROM [MyMoney].[RecurringTransactionDefinitions] rtd
    WHERE rtd.[UserId]   = @UserId
      AND rtd.[StatusId] = 1;    -- Active only
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 12. usp_RecurringTransaction_GetUpcomingByUser
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_GetUpcomingByUser', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_GetUpcomingByUser];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_GetUpcomingByUser]
    @UserId    BIGINT,
    @DaysAhead INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today    DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @Deadline DATE = DATEADD(DAY, @DaysAhead, @Today);

    SELECT
        rtd.[Id],
        rtd.[UserId],
        rtd.[Name],
        rtd.[Amount],
        rtd.[NextGenerationDate]            AS [NextDate],
        DATEDIFF(DAY, @Today, rtd.[NextGenerationDate]) AS [DaysUntil],
        c.[NameEn]                          AS [CategoryNameEn],
        c.[NameAr]                          AS [CategoryNameAr],
        rtd.[IsSubscription],
        sm.[ProviderName]
    FROM [MyMoney].[RecurringTransactionDefinitions] rtd
    INNER JOIN [MyMoney].[Categories] c ON c.[CategoryId] = rtd.[CategoryId]
    LEFT  JOIN [MyMoney].[SubscriptionMetadata] sm ON sm.[DefinitionId] = rtd.[Id]
    WHERE rtd.[UserId]   = @UserId
      AND rtd.[StatusId] = 1
      AND rtd.[NextGenerationDate] >= @Today
      AND rtd.[NextGenerationDate] <= @Deadline
    ORDER BY rtd.[NextGenerationDate] ASC;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 13. usp_RecurringTransaction_ExpireEnded
--     Batch-expires definitions whose EndDate has passed and were never
--     auto-expired during generation (e.g. due to scheduler downtime).
--     Called by the scheduler after the daily processing run.
-- ─────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_RecurringTransaction_ExpireEnded', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_RecurringTransaction_ExpireEnded];
GO
CREATE PROCEDURE [MyMoney].[usp_RecurringTransaction_ExpireEnded]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today DATE = CAST(GETUTCDATE() AS DATE);

    UPDATE [MyMoney].[RecurringTransactionDefinitions]
    SET [StatusId]     = 4,             -- Expired
        [UpdatedAtUtc] = GETUTCDATE()
    WHERE [StatusId] IN (1, 2)          -- Active or Paused
      AND [EndDate]   IS NOT NULL
      AND [EndDate]    < @Today;
END
GO
