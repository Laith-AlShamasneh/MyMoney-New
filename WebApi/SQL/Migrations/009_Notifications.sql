-- =============================================================================
-- Migration: 009_Notifications
-- Description: Enterprise in-app notification platform.
--              - CREATE TABLE: NotificationTemplates, NotificationTemplateTranslations,
--                              Notifications, UserNotificationPreferences
--              - Seed: 13 templates with EN/AR translations
--              - CREATE: 12 stored procedures
-- Author: Laith Al-Shamasneh
-- Date: 2026-06-17
-- =============================================================================


-- =============================================================================
-- 1. NotificationTemplates  (catalog — admin-managed)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NotificationTemplates' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[NotificationTemplates]
    (
        [TemplateId]  INT          NOT NULL IDENTITY(1, 1),
        [Code]        NVARCHAR(50) NOT NULL,
        [Category]    TINYINT      NOT NULL,   -- NotificationCategory enum
        [Type]        TINYINT      NOT NULL,   -- NotificationType enum
        [Priority]    TINYINT      NOT NULL,   -- NotificationPriority enum
        [IsActive]    BIT          NOT NULL CONSTRAINT [DF_NotificationTemplates_IsActive] DEFAULT 1,
        [CreatedAtUtc] DATETIME2(0) NOT NULL CONSTRAINT [DF_NotificationTemplates_CreatedAt] DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_NotificationTemplates]      PRIMARY KEY CLUSTERED ([TemplateId]),
        CONSTRAINT [UQ_NotificationTemplates_Code] UNIQUE ([Code]),
        CONSTRAINT [CK_NotificationTemplates_Category] CHECK ([Category] BETWEEN 1 AND 5),
        CONSTRAINT [CK_NotificationTemplates_Type]     CHECK ([Type]     BETWEEN 1 AND 5),
        CONSTRAINT [CK_NotificationTemplates_Priority] CHECK ([Priority] BETWEEN 1 AND 4)
    );
END
GO

-- =============================================================================
-- 2. NotificationTemplateTranslations  (one row per language per template)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NotificationTemplateTranslations' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[NotificationTemplateTranslations]
    (
        [TranslationId]   INT           NOT NULL IDENTITY(1, 1),
        [TemplateId]      INT           NOT NULL,
        [LanguageCode]    NVARCHAR(5)   NOT NULL,   -- 'en' | 'ar'
        [TitleTemplate]   NVARCHAR(200) NOT NULL,
        [MessageTemplate] NVARCHAR(500) NOT NULL,

        CONSTRAINT [PK_NotificationTemplateTranslations]    PRIMARY KEY CLUSTERED ([TranslationId]),
        CONSTRAINT [UQ_NTT_TemplateId_Language]             UNIQUE ([TemplateId], [LanguageCode]),
        CONSTRAINT [FK_NTT_Templates]
            FOREIGN KEY ([TemplateId]) REFERENCES [MyMoney].[NotificationTemplates]([TemplateId]) ON DELETE CASCADE
    );
END
GO

-- =============================================================================
-- 3. Notifications  (per-user instances, denormalized for fast reads)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Notifications' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[Notifications]
    (
        [NotificationId] BIGINT        NOT NULL IDENTITY(1, 1),
        [UserId]         BIGINT        NOT NULL,
        [TemplateId]     INT           NOT NULL,
        [Category]       TINYINT       NOT NULL,
        [Type]           TINYINT       NOT NULL,
        [Priority]       TINYINT       NOT NULL,
        [TitleEn]        NVARCHAR(200) NOT NULL,
        [TitleAr]        NVARCHAR(200) NOT NULL,
        [MessageEn]      NVARCHAR(500) NOT NULL,
        [MessageAr]      NVARCHAR(500) NOT NULL,
        [PayloadJson]    NVARCHAR(MAX) NULL,
        [Status]         TINYINT       NOT NULL CONSTRAINT [DF_Notifications_Status] DEFAULT 1,  -- 1=Unread
        [CreatedAtUtc]   DATETIME2(0)  NOT NULL CONSTRAINT [DF_Notifications_CreatedAt] DEFAULT GETUTCDATE(),
        [ReadAtUtc]      DATETIME2(0)  NULL,
        [ExpiresAtUtc]   DATETIME2(0)  NULL,

        CONSTRAINT [PK_Notifications] PRIMARY KEY CLUSTERED ([NotificationId]),
        CONSTRAINT [FK_Notifications_Users]     FOREIGN KEY ([UserId])     REFERENCES [MyMoney].[Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Notifications_Templates] FOREIGN KEY ([TemplateId]) REFERENCES [MyMoney].[NotificationTemplates]([TemplateId]),
        CONSTRAINT [CK_Notifications_Category] CHECK ([Category] BETWEEN 1 AND 5),
        CONSTRAINT [CK_Notifications_Type]     CHECK ([Type]     BETWEEN 1 AND 5),
        CONSTRAINT [CK_Notifications_Priority] CHECK ([Priority] BETWEEN 1 AND 4),
        CONSTRAINT [CK_Notifications_Status]   CHECK ([Status]   BETWEEN 1 AND 4)
    );

    CREATE NONCLUSTERED INDEX [IX_Notifications_UserId_Status]
        ON [MyMoney].[Notifications] ([UserId], [Status])
        INCLUDE ([Category], [Priority], [CreatedAtUtc]);

    CREATE NONCLUSTERED INDEX [IX_Notifications_UserId_Category]
        ON [MyMoney].[Notifications] ([UserId], [Category])
        INCLUDE ([Status], [CreatedAtUtc]);

    CREATE NONCLUSTERED INDEX [IX_Notifications_CreatedAtUtc]
        ON [MyMoney].[Notifications] ([CreatedAtUtc]);
END
GO

-- =============================================================================
-- 4. UserNotificationPreferences  (one row per user)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserNotificationPreferences' AND schema_id = SCHEMA_ID('MyMoney'))
BEGIN
    CREATE TABLE [MyMoney].[UserNotificationPreferences]
    (
        [UserId]           BIGINT       NOT NULL,
        [SecurityEnabled]  BIT          NOT NULL CONSTRAINT [DF_UNP_Security]  DEFAULT 1,
        [FinancialEnabled] BIT          NOT NULL CONSTRAINT [DF_UNP_Financial] DEFAULT 1,
        [SystemEnabled]    BIT          NOT NULL CONSTRAINT [DF_UNP_System]    DEFAULT 1,
        [ReportsEnabled]   BIT          NOT NULL CONSTRAINT [DF_UNP_Reports]   DEFAULT 1,
        [ProfileEnabled]   BIT          NOT NULL CONSTRAINT [DF_UNP_Profile]   DEFAULT 1,
        [UpdatedAtUtc]     DATETIME2(0) NOT NULL CONSTRAINT [DF_UNP_UpdatedAt] DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_UserNotificationPreferences] PRIMARY KEY CLUSTERED ([UserId]),
        CONSTRAINT [FK_UNP_Users] FOREIGN KEY ([UserId]) REFERENCES [MyMoney].[Users]([Id]) ON DELETE CASCADE
    );
END
GO


-- =============================================================================
-- 5. Seed  — NotificationTemplates + NotificationTemplateTranslations
-- =============================================================================

-- Templates
IF NOT EXISTS (SELECT 1 FROM [MyMoney].[NotificationTemplates] WHERE [Code] = 'Welcome')
BEGIN
    DECLARE @WelcomeId     INT, @PwdChangedId INT, @EmailChangedId INT,
            @SessRevokedId INT, @LargeTxId    INT, @BudgetExcId    INT,
            @BudgetNearId  INT, @RptReadyId   INT, @RptFailedId    INT,
            @ProfUpdId     INT, @ProfPicId    INT, @SysAnnId       INT,
            @MaintId       INT;

    -- Security (Category=1)
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('Welcome', 1, 1, 3); SET @WelcomeId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('PasswordChanged', 1, 3, 2); SET @PwdChangedId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('EmailChanged', 1, 3, 2); SET @EmailChangedId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('SessionRevoked', 1, 3, 3); SET @SessRevokedId = SCOPE_IDENTITY();

    -- Financial (Category=2)
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('LargeTransaction', 2, 3, 2); SET @LargeTxId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('BudgetExceeded', 2, 4, 2); SET @BudgetExcId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('BudgetNearingLimit', 2, 3, 3); SET @BudgetNearId = SCOPE_IDENTITY();

    -- Reports (Category=4)
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('ReportReady', 4, 2, 3); SET @RptReadyId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('ReportFailed', 4, 4, 2); SET @RptFailedId = SCOPE_IDENTITY();

    -- Profile (Category=5)
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('ProfileUpdated', 5, 2, 4); SET @ProfUpdId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('ProfilePictureChanged', 5, 2, 4); SET @ProfPicId = SCOPE_IDENTITY();

    -- System (Category=3)
    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('SystemAnnouncement', 3, 1, 3); SET @SysAnnId = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[NotificationTemplates] ([Code], [Category], [Type], [Priority])
    VALUES ('MaintenanceNotice', 3, 3, 2); SET @MaintId = SCOPE_IDENTITY();

    -- ── Translations ──────────────────────────────────────────────────────────

    INSERT INTO [MyMoney].[NotificationTemplateTranslations]
        ([TemplateId], [LanguageCode], [TitleTemplate], [MessageTemplate])
    VALUES
    -- Welcome
    (@WelcomeId,     'en', 'Welcome to MyMoney!',
                           'Your account has been created. Start tracking your finances today.'),
    (@WelcomeId,     'ar', N'مرحباً بك في ماي ماني!',
                           N'تم إنشاء حسابك بنجاح. ابدأ تتبع أموالك الآن.'),

    -- PasswordChanged
    (@PwdChangedId,  'en', 'Password Changed',
                           'Your password was changed on {ChangedAt}. If you did not do this, contact support immediately.'),
    (@PwdChangedId,  'ar', N'تم تغيير كلمة المرور',
                           N'تم تغيير كلمة مرورك بتاريخ {ChangedAt}. إذا لم تقم بذلك، يرجى التواصل مع الدعم فوراً.'),

    -- EmailChanged
    (@EmailChangedId,'en', 'Email Address Changed',
                           'Your email address was updated on {ChangedAt}. If this was not you, contact support immediately.'),
    (@EmailChangedId,'ar', N'تم تغيير عنوان البريد الإلكتروني',
                           N'تم تحديث بريدك الإلكتروني بتاريخ {ChangedAt}. إذا لم تقم بذلك، يرجى التواصل مع الدعم فوراً.'),

    -- SessionRevoked
    (@SessRevokedId, 'en', 'Session Revoked',
                           'A session was terminated from your account. If this was not you, please change your password.'),
    (@SessRevokedId, 'ar', N'تم إنهاء جلسة',
                           N'تم إنهاء جلسة نشطة في حسابك. إذا لم تقم بذلك، يرجى تغيير كلمة مرورك.'),

    -- LargeTransaction
    (@LargeTxId,     'en', 'Large Transaction Detected',
                           'A transaction of {Amount} was recorded on {Date}. Review your account if unexpected.'),
    (@LargeTxId,     'ar', N'تم رصد معاملة كبيرة',
                           N'تم تسجيل معاملة بمبلغ {Amount} بتاريخ {Date}. راجع حسابك إذا لم تكن متوقعة.'),

    -- BudgetExceeded
    (@BudgetExcId,   'en', 'Budget Exceeded',
                           'You have exceeded your budget for {Category} by {Amount}.'),
    (@BudgetExcId,   'ar', N'تم تجاوز الميزانية',
                           N'لقد تجاوزت ميزانية {Category} بمبلغ {Amount}.'),

    -- BudgetNearingLimit
    (@BudgetNearId,  'en', 'Approaching Budget Limit',
                           'You have used {Percentage}% of your {Category} budget.'),
    (@BudgetNearId,  'ar', N'اقتراب من حد الميزانية',
                           N'لقد استخدمت {Percentage}٪ من ميزانية {Category}.'),

    -- ReportReady
    (@RptReadyId,    'en', 'Report Ready',
                           'Your {ReportType} report has been generated and is ready to download.'),
    (@RptReadyId,    'ar', N'التقرير جاهز',
                           N'تم إنشاء تقرير {ReportType} وهو جاهز للتنزيل.'),

    -- ReportFailed
    (@RptFailedId,   'en', 'Report Generation Failed',
                           'We were unable to generate your report. Please try again or contact support.'),
    (@RptFailedId,   'ar', N'فشل إنشاء التقرير',
                           N'تعذّر إنشاء تقريرك. يرجى المحاولة مجدداً أو التواصل مع الدعم.'),

    -- ProfileUpdated
    (@ProfUpdId,     'en', 'Profile Updated',
                           'Your profile information has been updated successfully.'),
    (@ProfUpdId,     'ar', N'تم تعديل الملف الشخصي',
                           N'تم تعديل بيانات ملفك الشخصي بنجاح.'),

    -- ProfilePictureChanged
    (@ProfPicId,     'en', 'Profile Picture Updated',
                           'Your profile picture has been changed successfully.'),
    (@ProfPicId,     'ar', N'تم تغيير صورة الملف الشخصي',
                           N'تم تغيير صورة ملفك الشخصي بنجاح.'),

    -- SystemAnnouncement
    (@SysAnnId,      'en', '{Title}', '{Body}'),
    (@SysAnnId,      'ar', N'{Title}', N'{Body}'),

    -- MaintenanceNotice
    (@MaintId,       'en', 'Scheduled Maintenance',
                           'The system will be unavailable starting {StartTime} for approximately {Duration}.'),
    (@MaintId,       'ar', N'صيانة مجدولة',
                           N'سيكون النظام غير متاح ابتداءً من {StartTime} لمدة تقريبية {Duration}.');
END
GO


-- =============================================================================
-- 6. Stored Procedures
-- =============================================================================

-- ── usp_Notification_Create ───────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_Notification_Create', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Notification_Create];
GO
CREATE PROCEDURE [MyMoney].[usp_Notification_Create]
    @UserId       BIGINT,
    @TemplateId   INT,
    @Category     TINYINT,
    @Type         TINYINT,
    @Priority     TINYINT,
    @TitleEn      NVARCHAR(200),
    @TitleAr      NVARCHAR(200),
    @MessageEn    NVARCHAR(500),
    @MessageAr    NVARCHAR(500),
    @PayloadJson  NVARCHAR(MAX),
    @ExpiresAtUtc DATETIME2(0),
    @NewId        BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [MyMoney].[Notifications]
        ([UserId], [TemplateId], [Category], [Type], [Priority],
         [TitleEn], [TitleAr], [MessageEn], [MessageAr],
         [PayloadJson], [ExpiresAtUtc])
    VALUES
        (@UserId, @TemplateId, @Category, @Type, @Priority,
         @TitleEn, @TitleAr, @MessageEn, @MessageAr,
         @PayloadJson, @ExpiresAtUtc);
    SET @NewId = SCOPE_IDENTITY();
END
GO

-- ── usp_Notification_GetList ──────────────────────────────────────────────────
-- Result set 1: paginated notification rows
-- Result set 2: single row (TotalCount, UnreadCount)
IF OBJECT_ID('MyMoney.usp_Notification_GetList', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Notification_GetList];
GO
CREATE PROCEDURE [MyMoney].[usp_Notification_GetList]
    @UserId     BIGINT,
    @Status     TINYINT = NULL,
    @Category   TINYINT = NULL,
    @PageNumber INT     = 1,
    @PageSize   INT     = 20
AS
BEGIN
    SET NOCOUNT ON;

    -- Paginated rows
    SELECT
        [NotificationId], [Category], [Type], [Priority],
        [TitleEn], [TitleAr], [MessageEn], [MessageAr],
        [PayloadJson], [Status], [CreatedAtUtc], [ReadAtUtc]
    FROM [MyMoney].[Notifications]
    WHERE [UserId] = @UserId
      AND (@Status   IS NULL OR [Status]   = @Status)
      AND (@Category IS NULL OR [Category] = @Category)
      AND ([ExpiresAtUtc] IS NULL OR [ExpiresAtUtc] > GETUTCDATE())
    ORDER BY
        CASE [Priority] WHEN 1 THEN 0 WHEN 2 THEN 1 WHEN 3 THEN 2 ELSE 3 END,
        [CreatedAtUtc] DESC
    OFFSET  (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- Counts
    SELECT
        COUNT(*)                                                AS [TotalCount],
        SUM(CASE WHEN [Status] = 1 THEN 1 ELSE 0 END)          AS [UnreadCount]
    FROM [MyMoney].[Notifications]
    WHERE [UserId] = @UserId
      AND (@Status   IS NULL OR [Status]   = @Status)
      AND (@Category IS NULL OR [Category] = @Category)
      AND ([ExpiresAtUtc] IS NULL OR [ExpiresAtUtc] > GETUTCDATE());
END
GO

-- ── usp_Notification_GetUnreadCount ──────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_Notification_GetUnreadCount', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Notification_GetUnreadCount];
GO
CREATE PROCEDURE [MyMoney].[usp_Notification_GetUnreadCount]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*)
    FROM   [MyMoney].[Notifications]
    WHERE  [UserId] = @UserId
      AND  [Status] = 1
      AND  ([ExpiresAtUtc] IS NULL OR [ExpiresAtUtc] > GETUTCDATE());
END
GO

-- ── usp_Notification_MarkRead ─────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_Notification_MarkRead', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Notification_MarkRead];
GO
CREATE PROCEDURE [MyMoney].[usp_Notification_MarkRead]
    @UserId         BIGINT,
    @NotificationId BIGINT,
    @RowsAffected   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [MyMoney].[Notifications]
    SET    [Status] = 2, [ReadAtUtc] = GETUTCDATE()
    WHERE  [NotificationId] = @NotificationId
      AND  [UserId] = @UserId
      AND  [Status] = 1;
    SET @RowsAffected = @@ROWCOUNT;
END
GO

-- ── usp_Notification_MarkAllRead ─────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_Notification_MarkAllRead', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Notification_MarkAllRead];
GO
CREATE PROCEDURE [MyMoney].[usp_Notification_MarkAllRead]
    @UserId       BIGINT,
    @RowsAffected INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [MyMoney].[Notifications]
    SET    [Status] = 2, [ReadAtUtc] = GETUTCDATE()
    WHERE  [UserId] = @UserId
      AND  [Status] = 1;
    SET @RowsAffected = @@ROWCOUNT;
END
GO

-- ── usp_Notification_Archive ─────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_Notification_Archive', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Notification_Archive];
GO
CREATE PROCEDURE [MyMoney].[usp_Notification_Archive]
    @UserId         BIGINT,
    @NotificationId BIGINT,
    @RowsAffected   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [MyMoney].[Notifications]
    SET    [Status] = 3
    WHERE  [NotificationId] = @NotificationId
      AND  [UserId] = @UserId
      AND  [Status] IN (1, 2);
    SET @RowsAffected = @@ROWCOUNT;
END
GO

-- ── usp_Notification_Dismiss ─────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_Notification_Dismiss', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Notification_Dismiss];
GO
CREATE PROCEDURE [MyMoney].[usp_Notification_Dismiss]
    @UserId         BIGINT,
    @NotificationId BIGINT,
    @RowsAffected   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [MyMoney].[Notifications]
    SET    [Status] = 4
    WHERE  [NotificationId] = @NotificationId
      AND  [UserId] = @UserId
      AND  [Status] IN (1, 2, 3);
    SET @RowsAffected = @@ROWCOUNT;
END
GO

-- ── usp_Notification_Delete ───────────────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_Notification_Delete', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Notification_Delete];
GO
CREATE PROCEDURE [MyMoney].[usp_Notification_Delete]
    @UserId         BIGINT,
    @NotificationId BIGINT,
    @RowsAffected   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [MyMoney].[Notifications]
    WHERE  [NotificationId] = @NotificationId
      AND  [UserId] = @UserId;
    SET @RowsAffected = @@ROWCOUNT;
END
GO

-- ── usp_Notification_CleanupExpired ──────────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_Notification_CleanupExpired', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_Notification_CleanupExpired];
GO
CREATE PROCEDURE [MyMoney].[usp_Notification_CleanupExpired]
    @RetentionDays INT,
    @RowsDeleted   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @CutoffDate DATETIME2(0) = DATEADD(DAY, -@RetentionDays, GETUTCDATE());
    DELETE FROM [MyMoney].[Notifications]
    WHERE  [CreatedAtUtc] < @CutoffDate;
    SET @RowsDeleted = @@ROWCOUNT;
END
GO

-- ── usp_NotificationTemplate_GetByCode ───────────────────────────────────────
-- Result set 1: template row
-- Result set 2: translations list
IF OBJECT_ID('MyMoney.usp_NotificationTemplate_GetByCode', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_NotificationTemplate_GetByCode];
GO
CREATE PROCEDURE [MyMoney].[usp_NotificationTemplate_GetByCode]
    @Code NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [TemplateId], [Code], [Category], [Type], [Priority], [IsActive]
    FROM   [MyMoney].[NotificationTemplates]
    WHERE  [Code] = @Code;

    SELECT [LanguageCode], [TitleTemplate], [MessageTemplate]
    FROM   [MyMoney].[NotificationTemplateTranslations] t
    JOIN   [MyMoney].[NotificationTemplates] tmpl ON tmpl.[TemplateId] = t.[TemplateId]
    WHERE  tmpl.[Code] = @Code;
END
GO

-- ── usp_NotificationPreferences_GetOrInit ────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_NotificationPreferences_GetOrInit', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_NotificationPreferences_GetOrInit];
GO
CREATE PROCEDURE [MyMoney].[usp_NotificationPreferences_GetOrInit]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    MERGE [MyMoney].[UserNotificationPreferences] AS target
    USING (SELECT @UserId AS UserId) AS source ON target.[UserId] = source.UserId
    WHEN NOT MATCHED THEN
        INSERT ([UserId]) VALUES (@UserId);

    SELECT [SecurityEnabled], [FinancialEnabled], [SystemEnabled],
           [ReportsEnabled],  [ProfileEnabled]
    FROM   [MyMoney].[UserNotificationPreferences]
    WHERE  [UserId] = @UserId;
END
GO

-- ── usp_NotificationPreferences_Upsert ───────────────────────────────────────
IF OBJECT_ID('MyMoney.usp_NotificationPreferences_Upsert', 'P') IS NOT NULL
    DROP PROCEDURE [MyMoney].[usp_NotificationPreferences_Upsert];
GO
CREATE PROCEDURE [MyMoney].[usp_NotificationPreferences_Upsert]
    @UserId           BIGINT,
    @SecurityEnabled  BIT,
    @FinancialEnabled BIT,
    @SystemEnabled    BIT,
    @ReportsEnabled   BIT,
    @ProfileEnabled   BIT
AS
BEGIN
    SET NOCOUNT ON;

    MERGE [MyMoney].[UserNotificationPreferences] AS target
    USING (SELECT @UserId AS UserId) AS source ON target.[UserId] = source.UserId
    WHEN MATCHED THEN
        UPDATE SET
            [SecurityEnabled]  = @SecurityEnabled,
            [FinancialEnabled] = @FinancialEnabled,
            [SystemEnabled]    = @SystemEnabled,
            [ReportsEnabled]   = @ReportsEnabled,
            [ProfileEnabled]   = @ProfileEnabled,
            [UpdatedAtUtc]     = GETUTCDATE()
    WHEN NOT MATCHED THEN
        INSERT ([UserId], [SecurityEnabled], [FinancialEnabled],
                [SystemEnabled], [ReportsEnabled], [ProfileEnabled])
        VALUES (@UserId, @SecurityEnabled, @FinancialEnabled,
                @SystemEnabled, @ReportsEnabled, @ProfileEnabled);
END
GO
