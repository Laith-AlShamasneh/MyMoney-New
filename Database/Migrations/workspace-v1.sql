-- ============================================================
-- MyMoney – Workspace Platform v1 Migration
-- Date: 2026-06-23
-- ============================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ──────────────────────────────────────────────────────────────
-- TABLES
-- ──────────────────────────────────────────────────────────────

-- Workspace role definitions (system-level, seed data below)
CREATE TABLE [MyMoney].[WorkspaceRoles] (
    [RoleId]      [tinyint]     IDENTITY(1,1) NOT NULL,
    [Code]        [nvarchar](50)  NOT NULL,
    [NameEn]      [nvarchar](100) NOT NULL,
    [NameAr]      [nvarchar](100) NOT NULL,
    [Level]       [tinyint]     NOT NULL,   -- 1=Owner (highest), 7=Guest (lowest)
    [IsSystemRole][bit]         NOT NULL CONSTRAINT DF_WorkspaceRoles_IsSystem DEFAULT (1),
    CONSTRAINT [PK_WorkspaceRoles]      PRIMARY KEY CLUSTERED ([RoleId] ASC),
    CONSTRAINT [UQ_WorkspaceRoles_Code] UNIQUE NONCLUSTERED ([Code] ASC)
);
GO

-- Granular permission definitions (system-level, seed data below)
CREATE TABLE [MyMoney].[WorkspacePermissions] (
    [PermissionId]  [int]          IDENTITY(1,1) NOT NULL,
    [Code]          [nvarchar](100) NOT NULL,   -- e.g. Transactions.View
    [Resource]      [nvarchar](50)  NOT NULL,   -- Transactions | Members | Workspace …
    [Action]        [nvarchar](50)  NOT NULL,   -- View | Create | Edit | Delete …
    [DescriptionEn] [nvarchar](200) NOT NULL,
    [DescriptionAr] [nvarchar](200) NOT NULL,
    CONSTRAINT [PK_WorkspacePermissions]      PRIMARY KEY CLUSTERED ([PermissionId] ASC),
    CONSTRAINT [UQ_WorkspacePermissions_Code] UNIQUE NONCLUSTERED ([Code] ASC)
);
GO

-- Role ↔ Permission mapping
CREATE TABLE [MyMoney].[WorkspaceRolePermissions] (
    [RoleId]       [tinyint] NOT NULL,
    [PermissionId] [int]     NOT NULL,
    CONSTRAINT [PK_WorkspaceRolePermissions] PRIMARY KEY CLUSTERED ([RoleId] ASC, [PermissionId] ASC)
);
GO

-- Workspace (organisation / team / family / personal group)
CREATE TABLE [MyMoney].[Workspaces] (
    [WorkspaceId]   [bigint]       IDENTITY(1,1) NOT NULL,
    [OwnerUserId]   [bigint]       NOT NULL,
    [Name]          [nvarchar](100) NOT NULL,
    [Description]   [nvarchar](500) NULL,
    [TypeId]        [tinyint]      NOT NULL,   -- 1=Personal, 2=Family, 3=Business, 4=Team
    [CurrencyCode]  [nvarchar](10)  NULL,
    [Timezone]      [nvarchar](50)  NULL,
    [LogoFileName]  [nvarchar](500) NULL,
    [Color]         [nvarchar](10)  NULL,
    [IsActive]      [bit]          NOT NULL CONSTRAINT DF_Workspaces_IsActive DEFAULT (1),
    [CreatedAtUtc]  [datetime2](0)  NOT NULL,
    [UpdatedAtUtc]  [datetime2](0)  NULL,
    CONSTRAINT [PK_Workspaces] PRIMARY KEY CLUSTERED ([WorkspaceId] ASC)
);
GO

-- Workspace membership
CREATE TABLE [MyMoney].[WorkspaceMembers] (
    [MemberId]        [bigint]       IDENTITY(1,1) NOT NULL,
    [WorkspaceId]     [bigint]       NOT NULL,
    [UserId]          [bigint]       NOT NULL,
    [RoleId]          [tinyint]      NOT NULL,
    [StatusId]        [tinyint]      NOT NULL,   -- 1=Active, 2=Suspended, 3=Removed, 4=Left
    [InvitedByUserId] [bigint]       NULL,
    [JoinedAtUtc]     [datetime2](0)  NULL,
    [SuspendedAtUtc]  [datetime2](0)  NULL,
    [RemovedAtUtc]    [datetime2](0)  NULL,
    [LeftAtUtc]       [datetime2](0)  NULL,
    [CreatedAtUtc]    [datetime2](0)  NOT NULL,
    [UpdatedAtUtc]    [datetime2](0)  NULL,
    CONSTRAINT [PK_WorkspaceMembers]              PRIMARY KEY CLUSTERED ([MemberId] ASC),
    CONSTRAINT [UQ_WorkspaceMembers_Workspace_User] UNIQUE NONCLUSTERED ([WorkspaceId] ASC, [UserId] ASC)
);
GO

-- Workspace invitations (email-based)
CREATE TABLE [MyMoney].[WorkspaceInvitations] (
    [InvitationId]    [bigint]       IDENTITY(1,1) NOT NULL,
    [WorkspaceId]     [bigint]       NOT NULL,
    [InvitedByUserId] [bigint]       NOT NULL,
    [Email]           [nvarchar](256) NOT NULL,
    [RoleId]          [tinyint]      NOT NULL,
    [TokenHash]       [nvarchar](64)  NOT NULL,
    [StatusId]        [tinyint]      NOT NULL,   -- 1=Pending, 2=Accepted, 3=Rejected, 4=Expired, 5=Cancelled
    [ExpiresAtUtc]    [datetime2](0)  NOT NULL,
    [CreatedAtUtc]    [datetime2](0)  NOT NULL,
    [UpdatedAtUtc]    [datetime2](0)  NULL,
    [AcceptedAtUtc]   [datetime2](0)  NULL,
    [RejectedAtUtc]   [datetime2](0)  NULL,
    CONSTRAINT [PK_WorkspaceInvitations]             PRIMARY KEY CLUSTERED ([InvitationId] ASC),
    CONSTRAINT [UQ_WorkspaceInvitations_TokenHash]   UNIQUE NONCLUSTERED ([TokenHash] ASC)
);
GO

-- Workspace audit activity feed
CREATE TABLE [MyMoney].[WorkspaceActivity] (
    [ActivityId]   [bigint]        IDENTITY(1,1) NOT NULL,
    [WorkspaceId]  [bigint]        NOT NULL,
    [ActorUserId]  [bigint]        NOT NULL,
    [Action]       [nvarchar](100)  NOT NULL,
    [EntityType]   [nvarchar](50)   NULL,
    [EntityId]     [bigint]         NULL,
    [MetadataJson] [nvarchar](max)  NULL,
    [IpAddress]    [nvarchar](45)   NULL,
    [CreatedAtUtc] [datetime2](0)   NOT NULL,
    CONSTRAINT [PK_WorkspaceActivity] PRIMARY KEY CLUSTERED ([ActivityId] ASC)
);
GO

-- Per-user workspace switching preferences
CREATE TABLE [MyMoney].[UserWorkspacePreferences] (
    [UserId]             [bigint]       NOT NULL,
    [CurrentWorkspaceId] [bigint]       NULL,
    [DefaultWorkspaceId] [bigint]       NULL,
    [UpdatedAtUtc]       [datetime2](0)  NOT NULL,
    CONSTRAINT [PK_UserWorkspacePreferences] PRIMARY KEY CLUSTERED ([UserId] ASC)
);
GO

-- ──────────────────────────────────────────────────────────────
-- INDEXES
-- ──────────────────────────────────────────────────────────────

CREATE NONCLUSTERED INDEX [IX_Workspaces_OwnerUserId] ON [MyMoney].[Workspaces] ([OwnerUserId] ASC)
    INCLUDE ([Name],[TypeId],[IsActive]);
GO

CREATE NONCLUSTERED INDEX [IX_WorkspaceMembers_UserId_Status] ON [MyMoney].[WorkspaceMembers]
    ([UserId] ASC, [StatusId] ASC)
    INCLUDE ([WorkspaceId],[RoleId]);
GO

CREATE NONCLUSTERED INDEX [IX_WorkspaceMembers_WorkspaceId_Status] ON [MyMoney].[WorkspaceMembers]
    ([WorkspaceId] ASC, [StatusId] ASC)
    INCLUDE ([UserId],[RoleId]);
GO

CREATE NONCLUSTERED INDEX [IX_WorkspaceInvitations_Email_Status] ON [MyMoney].[WorkspaceInvitations]
    ([Email] ASC, [StatusId] ASC)
    INCLUDE ([WorkspaceId],[RoleId],[ExpiresAtUtc]);
GO

CREATE NONCLUSTERED INDEX [IX_WorkspaceInvitations_WorkspaceId_Status] ON [MyMoney].[WorkspaceInvitations]
    ([WorkspaceId] ASC, [StatusId] ASC)
    INCLUDE ([Email],[RoleId],[CreatedAtUtc]);
GO

CREATE NONCLUSTERED INDEX [IX_WorkspaceActivity_WorkspaceId_CreatedAt] ON [MyMoney].[WorkspaceActivity]
    ([WorkspaceId] ASC, [CreatedAtUtc] DESC)
    INCLUDE ([ActorUserId],[Action],[EntityType],[EntityId]);
GO

-- ──────────────────────────────────────────────────────────────
-- SEED: WorkspaceRoles
-- RoleId: 1=Owner, 2=Admin, 3=Manager, 4=Accountant, 5=Viewer, 6=Auditor, 7=Guest
-- ──────────────────────────────────────────────────────────────

SET IDENTITY_INSERT [MyMoney].[WorkspaceRoles] ON;
GO

INSERT INTO [MyMoney].[WorkspaceRoles] ([RoleId],[Code],[NameEn],[NameAr],[Level],[IsSystemRole]) VALUES
(1, 'Owner',       'Owner',       N'مالك',         1, 1),
(2, 'Admin',       'Admin',       N'مسؤول',         2, 1),
(3, 'Manager',     'Manager',     N'مدير',          3, 1),
(4, 'Accountant',  'Accountant',  N'محاسب',         4, 1),
(5, 'Viewer',      'Viewer',      N'مشاهد',         5, 1),
(6, 'Auditor',     'Auditor',     N'مدقق',          6, 1),
(7, 'Guest',       'Guest',       N'ضيف',           7, 1);
GO

SET IDENTITY_INSERT [MyMoney].[WorkspaceRoles] OFF;
GO

-- ──────────────────────────────────────────────────────────────
-- SEED: WorkspacePermissions
-- ──────────────────────────────────────────────────────────────

INSERT INTO [MyMoney].[WorkspacePermissions] ([Code],[Resource],[Action],[DescriptionEn],[DescriptionAr]) VALUES
-- Transactions
('Transactions.View',   'Transactions','View',   'View all transactions',            N'عرض جميع المعاملات'),
('Transactions.Create', 'Transactions','Create', 'Create new transactions',          N'إنشاء معاملات جديدة'),
('Transactions.Edit',   'Transactions','Edit',   'Edit existing transactions',        N'تعديل المعاملات الموجودة'),
('Transactions.Delete', 'Transactions','Delete', 'Delete transactions',              N'حذف المعاملات'),
-- Categories
('Categories.View',     'Categories',  'View',   'View categories',                  N'عرض الفئات'),
('Categories.Create',   'Categories',  'Create', 'Create new categories',            N'إنشاء فئات جديدة'),
('Categories.Edit',     'Categories',  'Edit',   'Edit existing categories',          N'تعديل الفئات الموجودة'),
('Categories.Delete',   'Categories',  'Delete', 'Delete categories',                N'حذف الفئات'),
-- Budgets
('Budgets.View',        'Budgets',     'View',   'View budgets',                     N'عرض الميزانيات'),
('Budgets.Create',      'Budgets',     'Create', 'Create new budgets',               N'إنشاء ميزانيات جديدة'),
('Budgets.Edit',        'Budgets',     'Edit',   'Edit existing budgets',             N'تعديل الميزانيات الموجودة'),
('Budgets.Delete',      'Budgets',     'Delete', 'Delete budgets',                   N'حذف الميزانيات'),
-- Goals
('Goals.View',          'Goals',       'View',   'View goals',                       N'عرض الأهداف'),
('Goals.Create',        'Goals',       'Create', 'Create new goals',                 N'إنشاء أهداف جديدة'),
('Goals.Edit',          'Goals',       'Edit',   'Edit existing goals',               N'تعديل الأهداف الموجودة'),
('Goals.Delete',        'Goals',       'Delete', 'Delete goals',                     N'حذف الأهداف'),
-- Reports
('Reports.View',        'Reports',     'View',   'View generated reports',           N'عرض التقارير المولّدة'),
('Reports.Generate',    'Reports',     'Generate','Generate new reports',            N'توليد تقارير جديدة'),
-- Receipts
('Receipts.View',       'Receipts',    'View',   'View receipts',                    N'عرض الإيصالات'),
('Receipts.Upload',     'Receipts',    'Upload', 'Upload new receipts',              N'رفع إيصالات جديدة'),
('Receipts.Delete',     'Receipts',    'Delete', 'Delete receipts',                  N'حذف الإيصالات'),
-- CashFlow
('CashFlow.View',       'CashFlow',    'View',   'View cash flow forecasts',         N'عرض توقعات التدفق النقدي'),
-- Calendar
('Calendar.View',       'Calendar',    'View',   'View calendar events',             N'عرض أحداث التقويم'),
-- Notifications
('Notifications.View',  'Notifications','View',  'View notifications',               N'عرض الإشعارات'),
-- Members
('Members.View',        'Members',     'View',   'View workspace members',           N'عرض أعضاء الفضاء'),
('Members.Invite',      'Members',     'Invite', 'Invite new members',               N'دعوة أعضاء جدد'),
('Members.ManageRoles', 'Members',     'ManageRoles','Change member roles',          N'تغيير أدوار الأعضاء'),
('Members.Suspend',     'Members',     'Suspend','Suspend members',                  N'تعليق الأعضاء'),
('Members.Remove',      'Members',     'Remove', 'Remove members from workspace',    N'إزالة الأعضاء'),
-- Workspace
('Workspace.View',      'Workspace',   'View',   'View workspace settings',          N'عرض إعدادات الفضاء'),
('Workspace.Edit',      'Workspace',   'Edit',   'Edit workspace details',            N'تعديل تفاصيل الفضاء'),
('Workspace.Delete',    'Workspace',   'Delete', 'Delete the workspace',             N'حذف الفضاء'),
('Workspace.ManageSettings','Workspace','ManageSettings','Manage workspace settings',N'إدارة إعدادات الفضاء'),
-- Invitations
('Invitations.View',    'Invitations', 'View',   'View sent invitations',            N'عرض الدعوات المُرسلة'),
('Invitations.Cancel',  'Invitations', 'Cancel', 'Cancel pending invitations',       N'إلغاء الدعوات المعلّقة');
GO

-- ──────────────────────────────────────────────────────────────
-- SEED: WorkspaceRolePermissions
-- Owner(1)=all, Admin(2)=all except Workspace.Delete,
-- Manager(3)=view+tx+cat+goals+budgets(no delete)+receipts(no delete)+cashflow+cal+notif+members.view+members.invite+inv.view+inv.cancel+ws.view
-- Accountant(4)=view+tx crud+receipts.upload+receipts.delete+reports.generate+cashflow+cal+notif+members.view+ws.view
-- Viewer(5)=view-only on most resources+ws.view+notif+members.view
-- Auditor(6)=viewer+reports.generate
-- Guest(7)=transactions.view+notifications.view+workspace.view
-- ──────────────────────────────────────────────────────────────

-- Helper: get PermissionId by Code
-- Owner (RoleId=1): ALL permissions
INSERT INTO [MyMoney].[WorkspaceRolePermissions] ([RoleId],[PermissionId])
SELECT 1, [PermissionId] FROM [MyMoney].[WorkspacePermissions];
GO

-- Admin (RoleId=2): ALL except Workspace.Delete
INSERT INTO [MyMoney].[WorkspaceRolePermissions] ([RoleId],[PermissionId])
SELECT 2, [PermissionId] FROM [MyMoney].[WorkspacePermissions]
WHERE [Code] <> 'Workspace.Delete';
GO

-- Manager (RoleId=3)
INSERT INTO [MyMoney].[WorkspaceRolePermissions] ([RoleId],[PermissionId])
SELECT 3, [PermissionId] FROM [MyMoney].[WorkspacePermissions]
WHERE [Code] IN (
    'Transactions.View','Transactions.Create','Transactions.Edit','Transactions.Delete',
    'Categories.View','Categories.Create','Categories.Edit','Categories.Delete',
    'Budgets.View','Budgets.Create','Budgets.Edit',
    'Goals.View','Goals.Create','Goals.Edit',
    'Reports.View','Reports.Generate',
    'Receipts.View','Receipts.Upload',
    'CashFlow.View','Calendar.View','Notifications.View',
    'Members.View','Members.Invite',
    'Workspace.View',
    'Invitations.View','Invitations.Cancel'
);
GO

-- Accountant (RoleId=4)
INSERT INTO [MyMoney].[WorkspaceRolePermissions] ([RoleId],[PermissionId])
SELECT 4, [PermissionId] FROM [MyMoney].[WorkspacePermissions]
WHERE [Code] IN (
    'Transactions.View','Transactions.Create','Transactions.Edit','Transactions.Delete',
    'Categories.View',
    'Budgets.View',
    'Goals.View',
    'Reports.View','Reports.Generate',
    'Receipts.View','Receipts.Upload','Receipts.Delete',
    'CashFlow.View','Calendar.View','Notifications.View',
    'Members.View',
    'Workspace.View'
);
GO

-- Viewer (RoleId=5)
INSERT INTO [MyMoney].[WorkspaceRolePermissions] ([RoleId],[PermissionId])
SELECT 5, [PermissionId] FROM [MyMoney].[WorkspacePermissions]
WHERE [Code] IN (
    'Transactions.View',
    'Categories.View',
    'Budgets.View',
    'Goals.View',
    'Reports.View',
    'Receipts.View',
    'CashFlow.View','Calendar.View','Notifications.View',
    'Members.View',
    'Workspace.View'
);
GO

-- Auditor (RoleId=6)
INSERT INTO [MyMoney].[WorkspaceRolePermissions] ([RoleId],[PermissionId])
SELECT 6, [PermissionId] FROM [MyMoney].[WorkspacePermissions]
WHERE [Code] IN (
    'Transactions.View',
    'Categories.View',
    'Budgets.View',
    'Goals.View',
    'Reports.View','Reports.Generate',
    'Receipts.View',
    'CashFlow.View','Calendar.View','Notifications.View',
    'Members.View',
    'Workspace.View'
);
GO

-- Guest (RoleId=7)
INSERT INTO [MyMoney].[WorkspaceRolePermissions] ([RoleId],[PermissionId])
SELECT 7, [PermissionId] FROM [MyMoney].[WorkspacePermissions]
WHERE [Code] IN (
    'Transactions.View',
    'Notifications.View',
    'Workspace.View'
);
GO

-- ──────────────────────────────────────────────────────────────
-- STORED PROCEDURES
-- ──────────────────────────────────────────────────────────────

-- ── usp_Workspace_Create ─────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Workspace_Create]
    @OwnerUserId  bigint,
    @Name         nvarchar(100),
    @Description  nvarchar(500) = NULL,
    @TypeId       tinyint,
    @CurrencyCode nvarchar(10) = NULL,
    @Timezone     nvarchar(50) = NULL,
    @Color        nvarchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now datetime2(0) = GETUTCDATE();
    DECLARE @WorkspaceId bigint;

    INSERT INTO [MyMoney].[Workspaces]
        ([OwnerUserId],[Name],[Description],[TypeId],[CurrencyCode],[Timezone],[Color],[IsActive],[CreatedAtUtc])
    VALUES
        (@OwnerUserId,@Name,@Description,@TypeId,@CurrencyCode,@Timezone,@Color,1,@Now);

    SET @WorkspaceId = SCOPE_IDENTITY();

    -- Add owner as member with Owner role (RoleId=1) and Active status (StatusId=1)
    INSERT INTO [MyMoney].[WorkspaceMembers]
        ([WorkspaceId],[UserId],[RoleId],[StatusId],[JoinedAtUtc],[CreatedAtUtc])
    VALUES
        (@WorkspaceId, @OwnerUserId, 1, 1, @Now, @Now);

    -- Upsert UserWorkspacePreferences to set this as current/default
    MERGE [MyMoney].[UserWorkspacePreferences] AS target
    USING (SELECT @OwnerUserId AS UserId) AS source ON (target.[UserId] = source.[UserId])
    WHEN MATCHED THEN
        UPDATE SET [CurrentWorkspaceId]=@WorkspaceId,[DefaultWorkspaceId]=ISNULL([DefaultWorkspaceId],@WorkspaceId),[UpdatedAtUtc]=@Now
    WHEN NOT MATCHED THEN
        INSERT ([UserId],[CurrentWorkspaceId],[DefaultWorkspaceId],[UpdatedAtUtc])
        VALUES (@OwnerUserId,@WorkspaceId,@WorkspaceId,@Now);

    -- Log activity
    INSERT INTO [MyMoney].[WorkspaceActivity]
        ([WorkspaceId],[ActorUserId],[Action],[EntityType],[EntityId],[CreatedAtUtc])
    VALUES (@WorkspaceId,@OwnerUserId,'Workspace.Created','Workspace',@WorkspaceId,@Now);

    SELECT @WorkspaceId AS WorkspaceId;
END
GO

-- ── usp_Workspace_Update ─────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Workspace_Update]
    @WorkspaceId  bigint,
    @CallerUserId bigint,
    @Name         nvarchar(100),
    @Description  nvarchar(500) = NULL,
    @CurrencyCode nvarchar(10) = NULL,
    @Timezone     nvarchar(50) = NULL,
    @Color        nvarchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Caller must be active Owner or Admin
    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1 AND [RoleId] IN (1,2)
    )
    BEGIN
        SELECT -1 AS AffectedRows;  -- Forbidden
        RETURN;
    END

    UPDATE [MyMoney].[Workspaces]
    SET [Name]=@Name, [Description]=@Description, [CurrencyCode]=@CurrencyCode,
        [Timezone]=@Timezone, [Color]=@Color, [UpdatedAtUtc]=GETUTCDATE()
    WHERE [WorkspaceId]=@WorkspaceId AND [IsActive]=1;

    DECLARE @Rows int = @@ROWCOUNT;

    IF @Rows > 0
        INSERT INTO [MyMoney].[WorkspaceActivity]
            ([WorkspaceId],[ActorUserId],[Action],[EntityType],[EntityId],[CreatedAtUtc])
        VALUES (@WorkspaceId,@CallerUserId,'Workspace.Updated','Workspace',@WorkspaceId,GETUTCDATE());

    SELECT @Rows AS AffectedRows;
END
GO

-- ── usp_Workspace_GetById ─────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Workspace_GetById]
    @WorkspaceId  bigint,
    @CallerUserId bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        w.[WorkspaceId], w.[OwnerUserId], w.[Name], w.[Description], w.[TypeId],
        w.[CurrencyCode], w.[Timezone], w.[LogoFileName], w.[Color],
        w.[IsActive], w.[CreatedAtUtc], w.[UpdatedAtUtc],
        m.[RoleId]   AS CallerRoleId,
        m.[StatusId] AS CallerStatusId,
        wr.[Code]    AS CallerRoleCode,
        (SELECT COUNT(*) FROM [MyMoney].[WorkspaceMembers]
         WHERE [WorkspaceId]=w.[WorkspaceId] AND [StatusId]=1) AS ActiveMemberCount
    FROM [MyMoney].[Workspaces] w
    LEFT JOIN [MyMoney].[WorkspaceMembers] m
        ON m.[WorkspaceId]=w.[WorkspaceId] AND m.[UserId]=@CallerUserId
    LEFT JOIN [MyMoney].[WorkspaceRoles] wr
        ON wr.[RoleId]=m.[RoleId]
    WHERE w.[WorkspaceId]=@WorkspaceId AND w.[IsActive]=1
        AND (
            w.[OwnerUserId]=@CallerUserId
            OR (m.[UserId] IS NOT NULL AND m.[StatusId]=1)
        );
END
GO

-- ── usp_Workspace_GetList ─────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Workspace_GetList]
    @CallerUserId bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        w.[WorkspaceId], w.[OwnerUserId], w.[Name], w.[TypeId],
        w.[CurrencyCode], w.[LogoFileName], w.[Color], w.[CreatedAtUtc],
        m.[RoleId], wr.[Code] AS RoleCode,
        (SELECT COUNT(*) FROM [MyMoney].[WorkspaceMembers] wm2
         WHERE wm2.[WorkspaceId]=w.[WorkspaceId] AND wm2.[StatusId]=1) AS ActiveMemberCount,
        CASE WHEN uwp.[CurrentWorkspaceId]=w.[WorkspaceId] THEN 1 ELSE 0 END AS IsCurrent,
        CASE WHEN uwp.[DefaultWorkspaceId]=w.[WorkspaceId] THEN 1 ELSE 0 END AS IsDefault
    FROM [MyMoney].[WorkspaceMembers] m
    INNER JOIN [MyMoney].[Workspaces] w ON w.[WorkspaceId]=m.[WorkspaceId] AND w.[IsActive]=1
    INNER JOIN [MyMoney].[WorkspaceRoles] wr ON wr.[RoleId]=m.[RoleId]
    LEFT JOIN  [MyMoney].[UserWorkspacePreferences] uwp ON uwp.[UserId]=@CallerUserId
    WHERE m.[UserId]=@CallerUserId AND m.[StatusId]=1
    ORDER BY w.[Name];
END
GO

-- ── usp_Workspace_Delete ─────────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Workspace_Delete]
    @WorkspaceId  bigint,
    @CallerUserId bigint
AS
BEGIN
    SET NOCOUNT ON;

    -- Only Owner (RoleId=1) can delete
    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1 AND [RoleId]=1
    )
    BEGIN
        SELECT -1 AS AffectedRows;  -- Forbidden
        RETURN;
    END

    UPDATE [MyMoney].[Workspaces]
    SET [IsActive]=0, [UpdatedAtUtc]=GETUTCDATE()
    WHERE [WorkspaceId]=@WorkspaceId;

    DECLARE @Rows int = @@ROWCOUNT;

    -- Clear current workspace preference for all members who had this set
    UPDATE [MyMoney].[UserWorkspacePreferences]
    SET [CurrentWorkspaceId]=NULL, [UpdatedAtUtc]=GETUTCDATE()
    WHERE [CurrentWorkspaceId]=@WorkspaceId;

    INSERT INTO [MyMoney].[WorkspaceActivity]
        ([WorkspaceId],[ActorUserId],[Action],[EntityType],[EntityId],[CreatedAtUtc])
    VALUES (@WorkspaceId,@CallerUserId,'Workspace.Deleted','Workspace',@WorkspaceId,GETUTCDATE());

    SELECT @Rows AS AffectedRows;
END
GO

-- ── usp_Workspace_SwitchCurrent ──────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Workspace_SwitchCurrent]
    @CallerUserId bigint,
    @WorkspaceId  bigint   -- NULL = personal (no workspace)
AS
BEGIN
    SET NOCOUNT ON;

    IF @WorkspaceId IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1
    )
    BEGIN
        SELECT 0 AS Success;
        RETURN;
    END

    MERGE [MyMoney].[UserWorkspacePreferences] AS target
    USING (SELECT @CallerUserId AS UserId) AS source ON (target.[UserId]=source.[UserId])
    WHEN MATCHED THEN
        UPDATE SET [CurrentWorkspaceId]=@WorkspaceId,[UpdatedAtUtc]=GETUTCDATE()
    WHEN NOT MATCHED THEN
        INSERT ([UserId],[CurrentWorkspaceId],[DefaultWorkspaceId],[UpdatedAtUtc])
        VALUES (@CallerUserId,@WorkspaceId,@WorkspaceId,GETUTCDATE());

    SELECT 1 AS Success;
END
GO

-- ── usp_WorkspaceMember_GetList ───────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceMember_GetList]
    @WorkspaceId  bigint,
    @CallerUserId bigint,
    @StatusId     tinyint = NULL    -- NULL = all statuses
AS
BEGIN
    SET NOCOUNT ON;

    -- Caller must be an active member
    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1
    ) RETURN;

    SELECT
        m.[MemberId], m.[WorkspaceId], m.[UserId], m.[RoleId],
        m.[StatusId], m.[InvitedByUserId], m.[JoinedAtUtc], m.[CreatedAtUtc],
        wr.[Code] AS RoleCode, wr.[NameEn] AS RoleNameEn, wr.[NameAr] AS RoleNameAr,
        p.[DisplayNameEn], p.[DisplayNameAr], p.[ProfilePicture],
        u.[Email]
    FROM [MyMoney].[WorkspaceMembers] m
    INNER JOIN [MyMoney].[WorkspaceRoles] wr ON wr.[RoleId]=m.[RoleId]
    INNER JOIN [MyMoney].[Users] u ON u.[UserId]=m.[UserId]
    INNER JOIN [MyMoney].[Persons] p ON p.[PersonId]=u.[PersonId]
    WHERE m.[WorkspaceId]=@WorkspaceId
        AND (@StatusId IS NULL OR m.[StatusId]=@StatusId)
    ORDER BY wr.[Level], p.[DisplayNameEn];
END
GO

-- ── usp_WorkspaceMember_UpdateRole ────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceMember_UpdateRole]
    @WorkspaceId  bigint,
    @CallerUserId bigint,
    @TargetUserId bigint,
    @NewRoleId    tinyint
AS
BEGIN
    SET NOCOUNT ON;

    -- Caller must be Owner(1) or Admin(2), and cannot demote Owner
    DECLARE @CallerRoleId tinyint = (
        SELECT [RoleId] FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1
    );
    DECLARE @TargetRoleId tinyint = (
        SELECT [RoleId] FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@TargetUserId AND [StatusId]=1
    );

    IF @CallerRoleId IS NULL OR @CallerRoleId > 2
        OR @TargetRoleId IS NULL OR @TargetRoleId = 1   -- cannot change Owner role
        OR @CallerUserId = @TargetUserId                -- cannot change own role
        OR @NewRoleId = 1                               -- cannot promote to Owner
    BEGIN
        SELECT -1 AS AffectedRows;
        RETURN;
    END

    UPDATE [MyMoney].[WorkspaceMembers]
    SET [RoleId]=@NewRoleId, [UpdatedAtUtc]=GETUTCDATE()
    WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@TargetUserId;

    DECLARE @Rows int = @@ROWCOUNT;

    IF @Rows > 0
        INSERT INTO [MyMoney].[WorkspaceActivity]
            ([WorkspaceId],[ActorUserId],[Action],[EntityType],[MetadataJson],[CreatedAtUtc])
        VALUES (@WorkspaceId,@CallerUserId,'Member.RoleChanged','Member',
                N'{"targetUserId":'+CAST(@TargetUserId AS nvarchar)+N',"newRoleId":'+CAST(@NewRoleId AS nvarchar)+N'}',
                GETUTCDATE());

    SELECT @Rows AS AffectedRows;
END
GO

-- ── usp_WorkspaceMember_Suspend ───────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceMember_Suspend]
    @WorkspaceId  bigint,
    @CallerUserId bigint,
    @TargetUserId bigint
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CallerRoleId tinyint = (
        SELECT [RoleId] FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1
    );
    DECLARE @TargetRoleId tinyint = (
        SELECT [RoleId] FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@TargetUserId AND [StatusId]=1
    );

    IF @CallerRoleId IS NULL OR @CallerRoleId > 2
        OR @TargetRoleId IS NULL OR @TargetRoleId = 1
        OR @CallerUserId = @TargetUserId
    BEGIN
        SELECT -1 AS AffectedRows;
        RETURN;
    END

    DECLARE @Now datetime2(0) = GETUTCDATE();
    UPDATE [MyMoney].[WorkspaceMembers]
    SET [StatusId]=2, [SuspendedAtUtc]=@Now, [UpdatedAtUtc]=@Now
    WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@TargetUserId AND [StatusId]=1;

    DECLARE @Rows int = @@ROWCOUNT;

    IF @Rows > 0
        INSERT INTO [MyMoney].[WorkspaceActivity]
            ([WorkspaceId],[ActorUserId],[Action],[EntityType],[MetadataJson],[CreatedAtUtc])
        VALUES (@WorkspaceId,@CallerUserId,'Member.Suspended','Member',
                N'{"targetUserId":'+CAST(@TargetUserId AS nvarchar)+N'}',@Now);

    SELECT @Rows AS AffectedRows;
END
GO

-- ── usp_WorkspaceMember_Reinstate ─────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceMember_Reinstate]
    @WorkspaceId  bigint,
    @CallerUserId bigint,
    @TargetUserId bigint
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CallerRoleId tinyint = (
        SELECT [RoleId] FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1
    );

    IF @CallerRoleId IS NULL OR @CallerRoleId > 2
    BEGIN
        SELECT -1 AS AffectedRows;
        RETURN;
    END

    DECLARE @Now datetime2(0) = GETUTCDATE();
    UPDATE [MyMoney].[WorkspaceMembers]
    SET [StatusId]=1, [SuspendedAtUtc]=NULL, [UpdatedAtUtc]=@Now
    WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@TargetUserId AND [StatusId]=2;

    DECLARE @Rows int = @@ROWCOUNT;

    IF @Rows > 0
        INSERT INTO [MyMoney].[WorkspaceActivity]
            ([WorkspaceId],[ActorUserId],[Action],[EntityType],[MetadataJson],[CreatedAtUtc])
        VALUES (@WorkspaceId,@CallerUserId,'Member.Reinstated','Member',
                N'{"targetUserId":'+CAST(@TargetUserId AS nvarchar)+N'}',@Now);

    SELECT @Rows AS AffectedRows;
END
GO

-- ── usp_WorkspaceMember_Remove ────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceMember_Remove]
    @WorkspaceId  bigint,
    @CallerUserId bigint,
    @TargetUserId bigint
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CallerRoleId tinyint = (
        SELECT [RoleId] FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1
    );
    DECLARE @TargetRoleId tinyint = (
        SELECT [RoleId] FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@TargetUserId AND [StatusId] IN (1,2)
    );

    IF @CallerRoleId IS NULL OR @CallerRoleId > 2
        OR @TargetRoleId IS NULL OR @TargetRoleId = 1
        OR @CallerUserId = @TargetUserId
    BEGIN
        SELECT -1 AS AffectedRows;
        RETURN;
    END

    DECLARE @Now datetime2(0) = GETUTCDATE();
    UPDATE [MyMoney].[WorkspaceMembers]
    SET [StatusId]=3, [RemovedAtUtc]=@Now, [UpdatedAtUtc]=@Now
    WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@TargetUserId;

    DECLARE @Rows int = @@ROWCOUNT;

    IF @Rows > 0
    BEGIN
        -- Clear current workspace pref if they were using this workspace
        UPDATE [MyMoney].[UserWorkspacePreferences]
        SET [CurrentWorkspaceId]=NULL, [UpdatedAtUtc]=@Now
        WHERE [UserId]=@TargetUserId AND [CurrentWorkspaceId]=@WorkspaceId;

        INSERT INTO [MyMoney].[WorkspaceActivity]
            ([WorkspaceId],[ActorUserId],[Action],[EntityType],[MetadataJson],[CreatedAtUtc])
        VALUES (@WorkspaceId,@CallerUserId,'Member.Removed','Member',
                N'{"targetUserId":'+CAST(@TargetUserId AS nvarchar)+N'}',@Now);
    END

    SELECT @Rows AS AffectedRows;
END
GO

-- ── usp_WorkspaceMember_Leave ─────────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceMember_Leave]
    @WorkspaceId  bigint,
    @CallerUserId bigint
AS
BEGIN
    SET NOCOUNT ON;

    -- Owner cannot leave (must transfer ownership first)
    DECLARE @RoleId tinyint = (
        SELECT [RoleId] FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1
    );

    IF @RoleId IS NULL OR @RoleId = 1
    BEGIN
        SELECT -1 AS AffectedRows;
        RETURN;
    END

    DECLARE @Now datetime2(0) = GETUTCDATE();
    UPDATE [MyMoney].[WorkspaceMembers]
    SET [StatusId]=4, [LeftAtUtc]=@Now, [UpdatedAtUtc]=@Now
    WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1;

    DECLARE @Rows int = @@ROWCOUNT;

    IF @Rows > 0
    BEGIN
        UPDATE [MyMoney].[UserWorkspacePreferences]
        SET [CurrentWorkspaceId]=NULL, [UpdatedAtUtc]=@Now
        WHERE [UserId]=@CallerUserId AND [CurrentWorkspaceId]=@WorkspaceId;

        INSERT INTO [MyMoney].[WorkspaceActivity]
            ([WorkspaceId],[ActorUserId],[Action],[EntityType],[MetadataJson],[CreatedAtUtc])
        VALUES (@WorkspaceId,@CallerUserId,'Member.Left','Member',
                N'{"userId":'+CAST(@CallerUserId AS nvarchar)+N'}',@Now);
    END

    SELECT @Rows AS AffectedRows;
END
GO

-- ── usp_WorkspaceMember_GetMyRole ─────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceMember_GetMyRole]
    @WorkspaceId  bigint,
    @CallerUserId bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        m.[MemberId], m.[RoleId], m.[StatusId],
        wr.[Code] AS RoleCode, wr.[Level] AS RoleLevel
    FROM [MyMoney].[WorkspaceMembers] m
    INNER JOIN [MyMoney].[WorkspaceRoles] wr ON wr.[RoleId]=m.[RoleId]
    WHERE m.[WorkspaceId]=@WorkspaceId AND m.[UserId]=@CallerUserId AND m.[StatusId]=1;
END
GO

-- ── usp_WorkspaceInvitation_Send ─────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceInvitation_Send]
    @WorkspaceId      bigint,
    @CallerUserId     bigint,
    @Email            nvarchar(256),
    @RoleId           tinyint,
    @TokenHash        nvarchar(64),
    @ExpiresAtUtc     datetime2(0)
AS
BEGIN
    SET NOCOUNT ON;

    -- Caller must have Members.Invite permission (RoleId <= 3)
    DECLARE @CallerRoleId tinyint = (
        SELECT [RoleId] FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1
    );

    IF @CallerRoleId IS NULL OR @CallerRoleId > 3
    BEGIN
        SELECT -1 AS InvitationId; -- Forbidden
        RETURN;
    END

    -- Cannot invite Owner role
    IF @RoleId = 1
    BEGIN
        SELECT -2 AS InvitationId; -- Invalid role
        RETURN;
    END

    -- Check if user with this email is already an active member
    IF EXISTS (
        SELECT 1 FROM [MyMoney].[WorkspaceMembers] m
        INNER JOIN [MyMoney].[Users] u ON u.[UserId]=m.[UserId]
        WHERE m.[WorkspaceId]=@WorkspaceId AND m.[StatusId]=1 AND u.[Email]=@Email
    )
    BEGIN
        SELECT -3 AS InvitationId; -- Already a member
        RETURN;
    END

    -- Cancel any existing pending invitation for this email+workspace
    UPDATE [MyMoney].[WorkspaceInvitations]
    SET [StatusId]=5, [UpdatedAtUtc]=GETUTCDATE()
    WHERE [WorkspaceId]=@WorkspaceId AND [Email]=@Email AND [StatusId]=1;

    DECLARE @Now datetime2(0) = GETUTCDATE();

    INSERT INTO [MyMoney].[WorkspaceInvitations]
        ([WorkspaceId],[InvitedByUserId],[Email],[RoleId],[TokenHash],[StatusId],[ExpiresAtUtc],[CreatedAtUtc])
    VALUES
        (@WorkspaceId,@CallerUserId,@Email,@RoleId,@TokenHash,1,@ExpiresAtUtc,@Now);

    DECLARE @InvitationId bigint = SCOPE_IDENTITY();

    INSERT INTO [MyMoney].[WorkspaceActivity]
        ([WorkspaceId],[ActorUserId],[Action],[EntityType],[EntityId],[MetadataJson],[CreatedAtUtc])
    VALUES (@WorkspaceId,@CallerUserId,'Invitation.Sent','Invitation',@InvitationId,
            N'{"email":"'+@Email+N'"}',@Now);

    SELECT @InvitationId AS InvitationId;
END
GO

-- ── usp_WorkspaceInvitation_GetByToken ───────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceInvitation_GetByToken]
    @TokenHash nvarchar(64)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        i.[InvitationId], i.[WorkspaceId], i.[InvitedByUserId], i.[Email],
        i.[RoleId], i.[StatusId], i.[ExpiresAtUtc], i.[CreatedAtUtc],
        wr.[Code] AS RoleCode,
        w.[Name]  AS WorkspaceName,
        w.[TypeId] AS WorkspaceTypeId,
        p.[DisplayNameEn] AS InviterNameEn,
        p.[DisplayNameAr] AS InviterNameAr
    FROM [MyMoney].[WorkspaceInvitations] i
    INNER JOIN [MyMoney].[WorkspaceRoles] wr ON wr.[RoleId]=i.[RoleId]
    INNER JOIN [MyMoney].[Workspaces] w ON w.[WorkspaceId]=i.[WorkspaceId]
    INNER JOIN [MyMoney].[Users] u ON u.[UserId]=i.[InvitedByUserId]
    INNER JOIN [MyMoney].[Persons] p ON p.[PersonId]=u.[PersonId]
    WHERE i.[TokenHash]=@TokenHash;
END
GO

-- ── usp_WorkspaceInvitation_Accept ───────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceInvitation_Accept]
    @TokenHash    nvarchar(64),
    @CallerUserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now datetime2(0) = GETUTCDATE();

    DECLARE @InvitationId bigint, @WorkspaceId bigint, @RoleId tinyint,
            @StatusId tinyint, @ExpiresAtUtc datetime2(0), @InvEmail nvarchar(256);

    SELECT
        @InvitationId=i.[InvitationId], @WorkspaceId=i.[WorkspaceId],
        @RoleId=i.[RoleId], @StatusId=i.[StatusId],
        @ExpiresAtUtc=i.[ExpiresAtUtc], @InvEmail=i.[Email]
    FROM [MyMoney].[WorkspaceInvitations] i
    WHERE i.[TokenHash]=@TokenHash;

    IF @InvitationId IS NULL
    BEGIN
        SELECT -1 AS Result; -- Not found
        RETURN;
    END

    IF @StatusId <> 1
    BEGIN
        SELECT -2 AS Result; -- Already processed
        RETURN;
    END

    IF @ExpiresAtUtc < @Now
    BEGIN
        UPDATE [MyMoney].[WorkspaceInvitations]
        SET [StatusId]=4, [UpdatedAtUtc]=@Now
        WHERE [InvitationId]=@InvitationId;
        SELECT -3 AS Result; -- Expired
        RETURN;
    END

    -- Verify caller's email matches invitation
    DECLARE @CallerEmail nvarchar(256) = (SELECT [Email] FROM [MyMoney].[Users] WHERE [UserId]=@CallerUserId);
    IF @CallerEmail <> @InvEmail
    BEGIN
        SELECT -4 AS Result; -- Email mismatch
        RETURN;
    END

    -- Accept
    UPDATE [MyMoney].[WorkspaceInvitations]
    SET [StatusId]=2, [AcceptedAtUtc]=@Now, [UpdatedAtUtc]=@Now
    WHERE [InvitationId]=@InvitationId;

    -- Add as member (or reactivate if removed/left)
    IF EXISTS (SELECT 1 FROM [MyMoney].[WorkspaceMembers]
               WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId)
    BEGIN
        UPDATE [MyMoney].[WorkspaceMembers]
        SET [StatusId]=1,[RoleId]=@RoleId,[JoinedAtUtc]=@Now,[UpdatedAtUtc]=@Now,
            [RemovedAtUtc]=NULL,[LeftAtUtc]=NULL,[SuspendedAtUtc]=NULL
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId;
    END
    ELSE
    BEGIN
        INSERT INTO [MyMoney].[WorkspaceMembers]
            ([WorkspaceId],[UserId],[RoleId],[StatusId],[InvitedByUserId],[JoinedAtUtc],[CreatedAtUtc])
        VALUES (@WorkspaceId,@CallerUserId,@RoleId,1,
                (SELECT [InvitedByUserId] FROM [MyMoney].[WorkspaceInvitations] WHERE [InvitationId]=@InvitationId),
                @Now,@Now);
    END

    INSERT INTO [MyMoney].[WorkspaceActivity]
        ([WorkspaceId],[ActorUserId],[Action],[EntityType],[EntityId],[CreatedAtUtc])
    VALUES (@WorkspaceId,@CallerUserId,'Member.Joined','Member',@CallerUserId,@Now);

    SELECT 1 AS Result; -- Success
END
GO

-- ── usp_WorkspaceInvitation_Reject ───────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceInvitation_Reject]
    @TokenHash    nvarchar(64),
    @CallerUserId bigint
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now datetime2(0) = GETUTCDATE();

    DECLARE @InvitationId bigint, @WorkspaceId bigint, @StatusId tinyint, @InvEmail nvarchar(256);

    SELECT @InvitationId=i.[InvitationId], @WorkspaceId=i.[WorkspaceId],
           @StatusId=i.[StatusId], @InvEmail=i.[Email]
    FROM [MyMoney].[WorkspaceInvitations] i
    WHERE i.[TokenHash]=@TokenHash;

    IF @InvitationId IS NULL
    BEGIN
        SELECT -1 AS Result;
        RETURN;
    END

    IF @StatusId <> 1
    BEGIN
        SELECT -2 AS Result;
        RETURN;
    END

    DECLARE @CallerEmail nvarchar(256) = (SELECT [Email] FROM [MyMoney].[Users] WHERE [UserId]=@CallerUserId);
    IF @CallerEmail <> @InvEmail
    BEGIN
        SELECT -4 AS Result;
        RETURN;
    END

    UPDATE [MyMoney].[WorkspaceInvitations]
    SET [StatusId]=3, [RejectedAtUtc]=@Now, [UpdatedAtUtc]=@Now
    WHERE [InvitationId]=@InvitationId;

    INSERT INTO [MyMoney].[WorkspaceActivity]
        ([WorkspaceId],[ActorUserId],[Action],[EntityType],[EntityId],[CreatedAtUtc])
    VALUES (@WorkspaceId,@CallerUserId,'Invitation.Rejected','Invitation',@InvitationId,@Now);

    SELECT 1 AS Result;
END
GO

-- ── usp_WorkspaceInvitation_Cancel ───────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceInvitation_Cancel]
    @InvitationId bigint,
    @WorkspaceId  bigint,
    @CallerUserId bigint
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CallerRoleId tinyint = (
        SELECT [RoleId] FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1
    );

    IF @CallerRoleId IS NULL OR @CallerRoleId > 3
    BEGIN
        SELECT -1 AS AffectedRows;
        RETURN;
    END

    DECLARE @Now datetime2(0) = GETUTCDATE();
    UPDATE [MyMoney].[WorkspaceInvitations]
    SET [StatusId]=5, [UpdatedAtUtc]=@Now
    WHERE [InvitationId]=@InvitationId AND [WorkspaceId]=@WorkspaceId AND [StatusId]=1;

    DECLARE @Rows int = @@ROWCOUNT;

    IF @Rows > 0
        INSERT INTO [MyMoney].[WorkspaceActivity]
            ([WorkspaceId],[ActorUserId],[Action],[EntityType],[EntityId],[CreatedAtUtc])
        VALUES (@WorkspaceId,@CallerUserId,'Invitation.Cancelled','Invitation',@InvitationId,@Now);

    SELECT @Rows AS AffectedRows;
END
GO

-- ── usp_WorkspaceInvitation_GetList ──────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceInvitation_GetList]
    @WorkspaceId  bigint,
    @CallerUserId bigint,
    @StatusId     tinyint = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1 AND [RoleId] <= 3
    ) RETURN;

    SELECT
        i.[InvitationId], i.[WorkspaceId], i.[Email], i.[RoleId],
        i.[StatusId], i.[ExpiresAtUtc], i.[CreatedAtUtc], i.[AcceptedAtUtc], i.[RejectedAtUtc],
        wr.[Code] AS RoleCode,
        p.[DisplayNameEn] AS InviterNameEn, p.[DisplayNameAr] AS InviterNameAr
    FROM [MyMoney].[WorkspaceInvitations] i
    INNER JOIN [MyMoney].[WorkspaceRoles] wr ON wr.[RoleId]=i.[RoleId]
    INNER JOIN [MyMoney].[Users] u ON u.[UserId]=i.[InvitedByUserId]
    INNER JOIN [MyMoney].[Persons] p ON p.[PersonId]=u.[PersonId]
    WHERE i.[WorkspaceId]=@WorkspaceId
        AND (@StatusId IS NULL OR i.[StatusId]=@StatusId)
    ORDER BY i.[CreatedAtUtc] DESC;
END
GO

-- ── usp_WorkspacePermission_GetMyPermissions ─────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspacePermission_GetMyPermissions]
    @WorkspaceId  bigint,
    @CallerUserId bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT wp.[Code], wp.[Resource], wp.[Action]
    FROM [MyMoney].[WorkspaceMembers] m
    INNER JOIN [MyMoney].[WorkspaceRolePermissions] rp ON rp.[RoleId]=m.[RoleId]
    INNER JOIN [MyMoney].[WorkspacePermissions] wp ON wp.[PermissionId]=rp.[PermissionId]
    WHERE m.[WorkspaceId]=@WorkspaceId AND m.[UserId]=@CallerUserId AND m.[StatusId]=1;
END
GO

-- ── usp_WorkspaceActivity_GetList ─────────────────────────────
CREATE OR ALTER PROCEDURE [MyMoney].[usp_WorkspaceActivity_GetList]
    @WorkspaceId  bigint,
    @CallerUserId bigint,
    @PageNumber   int = 1,
    @PageSize     int = 30
AS
BEGIN
    SET NOCOUNT ON;

    -- Caller must be an active member
    IF NOT EXISTS (
        SELECT 1 FROM [MyMoney].[WorkspaceMembers]
        WHERE [WorkspaceId]=@WorkspaceId AND [UserId]=@CallerUserId AND [StatusId]=1
    ) RETURN;

    DECLARE @Offset int = (@PageNumber - 1) * @PageSize;

    SELECT COUNT(*) AS TotalCount
    FROM [MyMoney].[WorkspaceActivity]
    WHERE [WorkspaceId]=@WorkspaceId;

    SELECT
        a.[ActivityId], a.[WorkspaceId], a.[ActorUserId], a.[Action],
        a.[EntityType], a.[EntityId], a.[MetadataJson], a.[CreatedAtUtc],
        p.[DisplayNameEn] AS ActorNameEn, p.[DisplayNameAr] AS ActorNameAr,
        per.[ProfilePicture] AS ActorProfilePicture
    FROM [MyMoney].[WorkspaceActivity] a
    INNER JOIN [MyMoney].[Users] u ON u.[UserId]=a.[ActorUserId]
    INNER JOIN [MyMoney].[Persons] per ON per.[PersonId]=u.[PersonId]
    CROSS APPLY (SELECT per.[DisplayNameEn],per.[DisplayNameAr]) p
    WHERE a.[WorkspaceId]=@WorkspaceId
    ORDER BY a.[CreatedAtUtc] DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ── usp_Workspace_GetCurrentContext ───────────────────────────
-- Returns the user's current workspace context (null if personal mode)
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Workspace_GetCurrentContext]
    @CallerUserId bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        uwp.[CurrentWorkspaceId], uwp.[DefaultWorkspaceId],
        w.[Name] AS WorkspaceName, w.[TypeId] AS WorkspaceTypeId,
        w.[CurrencyCode], w.[LogoFileName], w.[Color],
        m.[RoleId], wr.[Code] AS RoleCode, wr.[Level] AS RoleLevel
    FROM [MyMoney].[UserWorkspacePreferences] uwp
    LEFT JOIN [MyMoney].[Workspaces] w ON w.[WorkspaceId]=uwp.[CurrentWorkspaceId] AND w.[IsActive]=1
    LEFT JOIN [MyMoney].[WorkspaceMembers] m
        ON m.[WorkspaceId]=uwp.[CurrentWorkspaceId] AND m.[UserId]=@CallerUserId AND m.[StatusId]=1
    LEFT JOIN [MyMoney].[WorkspaceRoles] wr ON wr.[RoleId]=m.[RoleId]
    WHERE uwp.[UserId]=@CallerUserId;
END
GO
