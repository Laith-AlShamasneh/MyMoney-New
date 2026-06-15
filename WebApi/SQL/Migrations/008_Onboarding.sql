-- =============================================================================
-- Migration: 008_Onboarding
-- Description: Adds first-time user onboarding tracking.
--              - ALTER Users: adds OnboardingCompletedAtUtc column
--              - CREATE TABLE: OnboardingSteps, UserOnboarding, UserOnboardingSteps
--              - DROP/CREATE: usp_Authentication_Login (adds OnboardingCompletedAtUtc)
--              - DROP/CREATE: usp_Authentication_RefreshToken (adds OnboardingCompletedAtUtc)
--              - CREATE: usp_Onboarding_Initialize, GetState, AdvanceStep, Skip
-- Author: Laith Al-Shamasneh
-- Date: 2026-06-15
-- =============================================================================

-- =============================================================================
-- 1. Alter Users table
-- =============================================================================
ALTER TABLE [MyMoney].[Users]
    ADD [OnboardingCompletedAtUtc] DATETIME2(0) NULL;
GO

-- =============================================================================
-- 2. OnboardingSteps — catalog / seed data (admin-managed, not user-specific)
-- =============================================================================
CREATE TABLE [MyMoney].[OnboardingSteps]
(
    [StepId]    INT            NOT NULL IDENTITY(1, 1),
    [StepKey]   NVARCHAR(50)   NOT NULL,
    [NameEn]    NVARCHAR(100)  NOT NULL,
    [NameAr]    NVARCHAR(100)  NOT NULL,
    [SortOrder] TINYINT        NOT NULL,
    [IsRequired] BIT           NOT NULL CONSTRAINT [DF_OnboardingSteps_IsRequired] DEFAULT 1,
    [CanSkip]   BIT            NOT NULL CONSTRAINT [DF_OnboardingSteps_CanSkip]    DEFAULT 0,
    [PagePath]  NVARCHAR(200)  NOT NULL,

    CONSTRAINT [PK_OnboardingSteps]          PRIMARY KEY CLUSTERED ([StepId]),
    CONSTRAINT [UQ_OnboardingSteps_StepKey]  UNIQUE ([StepKey]),
    CONSTRAINT [UQ_OnboardingSteps_Sort]     UNIQUE ([SortOrder])
);
GO

INSERT INTO [MyMoney].[OnboardingSteps]
    ([StepKey], [NameEn], [NameAr], [SortOrder], [IsRequired], [CanSkip], [PagePath])
VALUES
    ('welcome',           N'Welcome to MyMoney',          N'مرحباً في ماي ماني',       1, 1, 0, '/pages/dashboard/index.html'),
    ('add_category',      N'Create a Category',           N'أضف تصنيفاً',              2, 0, 1, '/pages/dashboard/settings.html'),
    ('first_transaction', N'Add Your First Transaction',  N'أضف معاملتك الأولى',       3, 0, 1, '/pages/transactions/index.html'),
    ('explore_dashboard', N'Explore the Dashboard',       N'استكشف لوحة التحكم',       4, 1, 0, '/pages/dashboard/index.html'),
    ('complete',          N'Setup Complete',              N'اكتمل الإعداد',             5, 1, 0, '/pages/dashboard/index.html');
GO

-- =============================================================================
-- 3. UserOnboarding — one row per user; tracks overall progress
-- =============================================================================
CREATE TABLE [MyMoney].[UserOnboarding]
(
    [UserOnboardingId] BIGINT       NOT NULL IDENTITY(1, 1),
    [UserId]           BIGINT       NOT NULL,
    [Status]           TINYINT      NOT NULL CONSTRAINT [DF_UserOnboarding_Status]         DEFAULT 0,
    [CurrentStepKey]   NVARCHAR(50) NOT NULL CONSTRAINT [DF_UserOnboarding_CurrentStepKey] DEFAULT 'welcome',
    [StartedAtUtc]     DATETIME2(0) NOT NULL CONSTRAINT [DF_UserOnboarding_StartedAtUtc]   DEFAULT GETUTCDATE(),
    [LastUpdatedAtUtc] DATETIME2(0) NOT NULL CONSTRAINT [DF_UserOnboarding_LastUpdatedAt]  DEFAULT GETUTCDATE(),
    [CompletedAtUtc]   DATETIME2(0) NULL,

    CONSTRAINT [PK_UserOnboarding]        PRIMARY KEY CLUSTERED ([UserOnboardingId]),
    CONSTRAINT [UQ_UserOnboarding_UserId] UNIQUE ([UserId]),
    CONSTRAINT [FK_UserOnboarding_Users]  FOREIGN KEY ([UserId])
        REFERENCES [MyMoney].[Users]([UserId]) ON DELETE CASCADE,
    CONSTRAINT [CK_UserOnboarding_Status] CHECK ([Status] IN (0, 1, 3))
    -- 0=InProgress  1=Completed  3=Skipped
);
GO

-- =============================================================================
-- 4. UserOnboardingSteps — one row per (user, step)
-- =============================================================================
CREATE TABLE [MyMoney].[UserOnboardingSteps]
(
    [UserOnboardingStepId] BIGINT       NOT NULL IDENTITY(1, 1),
    [UserOnboardingId]     BIGINT       NOT NULL,
    [StepId]               INT          NOT NULL,
    [Status]               TINYINT      NOT NULL CONSTRAINT [DF_UserOnboardingSteps_Status] DEFAULT 0,
    [StartedAtUtc]         DATETIME2(0) NULL,
    [CompletedAtUtc]       DATETIME2(0) NULL,

    CONSTRAINT [PK_UserOnboardingSteps]                   PRIMARY KEY CLUSTERED ([UserOnboardingStepId]),
    CONSTRAINT [UQ_UserOnboardingSteps_UserStep]           UNIQUE ([UserOnboardingId], [StepId]),
    CONSTRAINT [FK_UserOnboardingSteps_UserOnboarding]     FOREIGN KEY ([UserOnboardingId])
        REFERENCES [MyMoney].[UserOnboarding]([UserOnboardingId]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserOnboardingSteps_OnboardingSteps]    FOREIGN KEY ([StepId])
        REFERENCES [MyMoney].[OnboardingSteps]([StepId]),
    CONSTRAINT [CK_UserOnboardingSteps_Status]             CHECK ([Status] IN (0, 1, 2, 3))
    -- 0=Pending  1=InProgress  2=Completed  3=Skipped
);
GO

CREATE INDEX [IX_UserOnboardingSteps_UserOnboardingId]
    ON [MyMoney].[UserOnboardingSteps] ([UserOnboardingId]);
GO

-- =============================================================================
-- 5. Modify usp_Authentication_Login — add OnboardingCompletedAtUtc to SELECT
-- =============================================================================
DROP PROCEDURE IF EXISTS [MyMoney].[usp_Authentication_Login];
GO

-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_Login
-- Description: Retrieves full user record for login validation.
--              Returns NULL row if email not found or account deleted.
-- ============================================================
CREATE PROCEDURE [MyMoney].[usp_Authentication_Login]
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.PersonId,
        u.Email,
        p.DisplayNameEn,
        p.DisplayNameAr,
        p.ProfilePicture,
        u.PasswordHash,
        u.IsActive,
        u.IsEmailConfirmed,
        u.IsLocked,
        u.LockoutEndDateUtc,
        u.FailedLoginAttempts,
        ur.RoleId,
        r.NameEn                    AS RoleNameEn,
        r.NameAr                    AS RoleNameAr,
        u.OnboardingCompletedAtUtc
    FROM             [MyMoney].[Users]     u
    INNER JOIN       [MyMoney].[Persons]   p  ON  p.PersonId = u.PersonId
    INNER JOIN       [MyMoney].[UserRoles] ur ON ur.UserId   = u.UserId
    INNER JOIN       [MyMoney].[Roles]     r  ON  r.RoleId   = ur.RoleId
    WHERE     u.Email = @Email;
END;
GO

-- =============================================================================
-- 6. Modify usp_Authentication_RefreshToken — add OnboardingCompletedAtUtc
-- =============================================================================
DROP PROCEDURE IF EXISTS [MyMoney].[usp_Authentication_RefreshToken];
GO

-- =====================================================================
-- MyMoney.usp_Authentication_RefreshToken
-- Atomically rotates a refresh token.
--
-- Validates the old token, revokes it, inserts the new one, and returns
-- the user's profile data in a single round-trip so the application can
-- build a fresh JWT + refresh-token response without extra lookups.
--
-- ResultCode:
--     0 = Success   — old token revoked, new token inserted, user data returned
--     1 = Invalid   — token not found, already revoked, or user inactive/deleted
--     2 = Expired   — token was valid but past its ExpiresOnUtc
-- =====================================================================
CREATE PROCEDURE [MyMoney].[usp_Authentication_RefreshToken]
    @OldTokenHash       NVARCHAR(512),
    @NewTokenHash       NVARCHAR(512),
    @NewExpiresOnUtc    DATETIME2(0),
    @RevokedByIp        NVARCHAR(50)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- %% 1. Look up the old token %%
    DECLARE
        @UserId             BIGINT,
        @RevokedOnUtc       DATETIME2(0),
        @ExpiresOnUtc       DATETIME2(0);

    SELECT
        @UserId           = rt.UserId,
        @RevokedOnUtc     = rt.RevokedOnUtc,
        @ExpiresOnUtc     = rt.ExpiresOnUtc
    FROM [MyMoney].[RefreshTokens] rt
    WHERE rt.Token = @OldTokenHash;

    -- %% 2. Token not found %%
    IF @UserId IS NULL
    BEGIN
        SELECT
            1                               AS ResultCode,
            CAST(0 AS BIGINT)               AS UserId,
            NULL                            AS Email,
            NULL                            AS DisplayNameEn,
            NULL                            AS DisplayNameAr,
            NULL                            AS ProfilePicture,
            CAST(0 AS INT)                  AS RoleId,
            NULL                            AS RoleNameEn,
            NULL                            AS RoleNameAr,
            CAST(NULL AS DATETIME2(0))      AS OnboardingCompletedAtUtc;
        RETURN;
    END

    -- %% 3. Already revoked %%
    IF @RevokedOnUtc IS NOT NULL
    BEGIN
        SELECT
            1                               AS ResultCode,
            CAST(0 AS BIGINT)               AS UserId,
            NULL                            AS Email,
            NULL                            AS DisplayNameEn,
            NULL                            AS DisplayNameAr,
            NULL                            AS ProfilePicture,
            CAST(0 AS INT)                  AS RoleId,
            NULL                            AS RoleNameEn,
            NULL                            AS RoleNameAr,
            CAST(NULL AS DATETIME2(0))      AS OnboardingCompletedAtUtc;
        RETURN;
    END

    -- %% 4. Expired %%
    IF @ExpiresOnUtc < GETUTCDATE()
    BEGIN
        SELECT
            2                               AS ResultCode,
            CAST(0 AS BIGINT)               AS UserId,
            NULL                            AS Email,
            NULL                            AS DisplayNameEn,
            NULL                            AS DisplayNameAr,
            NULL                            AS ProfilePicture,
            CAST(0 AS INT)                  AS RoleId,
            NULL                            AS RoleNameEn,
            NULL                            AS RoleNameAr,
            CAST(NULL AS DATETIME2(0))      AS OnboardingCompletedAtUtc;
        RETURN;
    END

    -- %% 5. Verify user is still active %%
    IF NOT EXISTS (
        SELECT 1
        FROM [MyMoney].[Users]
        WHERE UserId     = @UserId
          AND IsActive   = 1
    )
    BEGIN
        SELECT
            1                               AS ResultCode,
            CAST(0 AS BIGINT)               AS UserId,
            NULL                            AS Email,
            NULL                            AS DisplayNameEn,
            NULL                            AS DisplayNameAr,
            NULL                            AS ProfilePicture,
            CAST(0 AS INT)                  AS RoleId,
            NULL                            AS RoleNameEn,
            NULL                            AS RoleNameAr,
            CAST(NULL AS DATETIME2(0))      AS OnboardingCompletedAtUtc;
        RETURN;
    END

    -- %% 6. Rotate: revoke old, insert new %%
    UPDATE [MyMoney].[RefreshTokens]
    SET
        RevokedOnUtc       = GETUTCDATE(),
        RevokedByIp        = @RevokedByIp,
        ReasonRevoked      = 'Rotation',
        ReplacedByToken    = @NewTokenHash
    WHERE Token = @OldTokenHash;

    INSERT INTO [MyMoney].[RefreshTokens] (UserId, Token, ExpiresOnUtc, CreatedOnUtc, CreatedByIp)
    VALUES (@UserId, @NewTokenHash, @NewExpiresOnUtc, GETUTCDATE(), @RevokedByIp);

    -- %% 7. Return user data for response building %%
    SELECT
        0                               AS ResultCode,
        u.UserId,
        u.Email,
        p.DisplayNameEn,
        p.DisplayNameAr,
        p.ProfilePicture,
        ur.RoleId,
        r.NameEn                        AS RoleNameEn,
        r.NameAr                        AS RoleNameAr,
        u.OnboardingCompletedAtUtc
    FROM             [MyMoney].[Users]     u
    INNER JOIN       [MyMoney].[Persons]   p  ON  p.PersonId = u.PersonId
    INNER JOIN       [MyMoney].[UserRoles] ur ON ur.UserId   = u.UserId
    INNER JOIN       [MyMoney].[Roles]     r  ON  r.RoleId   = ur.RoleId
    WHERE u.UserId = @UserId;
END;
GO

-- =============================================================================
-- 7. usp_Onboarding_Initialize
-- =============================================================================
-- ============================================================
-- MyMoney.usp_Onboarding_Initialize
-- Creates the UserOnboarding row and the first step (welcome)
-- as InProgress for a newly registered user.
-- Idempotent: does nothing if the row already exists.
-- ============================================================
CREATE PROCEDURE [MyMoney].[usp_Onboarding_Initialize]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM [MyMoney].[UserOnboarding] WHERE [UserId] = @UserId)
        RETURN;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO [MyMoney].[UserOnboarding]
            ([UserId], [Status], [CurrentStepKey], [StartedAtUtc], [LastUpdatedAtUtc])
        VALUES
            (@UserId, 0, 'welcome', GETUTCDATE(), GETUTCDATE());

        DECLARE @UserOnboardingId BIGINT = SCOPE_IDENTITY();

        INSERT INTO [MyMoney].[UserOnboardingSteps]
            ([UserOnboardingId], [StepId], [Status], [StartedAtUtc])
        SELECT @UserOnboardingId, [StepId], 1, GETUTCDATE()
        FROM   [MyMoney].[OnboardingSteps]
        WHERE  [StepKey] = 'welcome';

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- =============================================================================
-- 8. usp_Onboarding_GetState
-- =============================================================================
-- ============================================================
-- MyMoney.usp_Onboarding_GetState
-- Returns full onboarding state for a user (get-or-create).
-- Returns one row per OnboardingStep (5 rows total).
-- ============================================================
CREATE PROCEDURE [MyMoney].[usp_Onboarding_GetState]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- Lazy create if InitializeAsync was never called (edge-case fault tolerance)
    IF NOT EXISTS (SELECT 1 FROM [MyMoney].[UserOnboarding] WHERE [UserId] = @UserId)
    BEGIN
        DECLARE @NewId BIGINT;

        INSERT INTO [MyMoney].[UserOnboarding]
            ([UserId], [Status], [CurrentStepKey], [StartedAtUtc], [LastUpdatedAtUtc])
        VALUES
            (@UserId, 0, 'welcome', GETUTCDATE(), GETUTCDATE());

        SET @NewId = SCOPE_IDENTITY();

        INSERT INTO [MyMoney].[UserOnboardingSteps]
            ([UserOnboardingId], [StepId], [Status], [StartedAtUtc])
        SELECT @NewId, [StepId], 1, GETUTCDATE()
        FROM   [MyMoney].[OnboardingSteps]
        WHERE  [StepKey] = 'welcome';
    END

    SELECT
        uo.[CurrentStepKey],
        uo.[Status]             AS OnboardingStatus,
        uo.[StartedAtUtc],
        uo.[LastUpdatedAtUtc],
        uo.[CompletedAtUtc],
        os.[StepKey],
        os.[NameEn],
        os.[NameAr],
        os.[SortOrder],
        os.[IsRequired],
        os.[CanSkip],
        os.[PagePath],
        uos.[Status]            AS StepStatus,
        uos.[StartedAtUtc]      AS StepStartedAtUtc,
        uos.[CompletedAtUtc]    AS StepCompletedAtUtc
    FROM           [MyMoney].[UserOnboarding]      uo
    CROSS JOIN     [MyMoney].[OnboardingSteps]     os
    LEFT JOIN      [MyMoney].[UserOnboardingSteps] uos
        ON  uos.[UserOnboardingId] = uo.[UserOnboardingId]
        AND uos.[StepId]           = os.[StepId]
    WHERE  uo.[UserId] = @UserId
    ORDER BY os.[SortOrder];
END;
GO

-- =============================================================================
-- 9. usp_Onboarding_AdvanceStep
-- =============================================================================
-- ============================================================
-- MyMoney.usp_Onboarding_AdvanceStep
-- Marks a step as Completed (2) or Skipped (3), advances
-- CurrentStepKey, and finalises onboarding when the last step
-- is done.
--
-- ResultCode:
--     0 = Success
--     1 = UserOnboarding row not found
--     2 = StepKey not found
-- ============================================================
CREATE PROCEDURE [MyMoney].[usp_Onboarding_AdvanceStep]
    @UserId     BIGINT,
    @StepKey    NVARCHAR(50),
    @StepStatus TINYINT   -- 2 = Completed, 3 = Skipped
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserOnboardingId BIGINT;
    DECLARE @StepId           INT;
    DECLARE @CurrentSortOrder TINYINT;

    SELECT @UserOnboardingId = [UserOnboardingId]
    FROM   [MyMoney].[UserOnboarding]
    WHERE  [UserId] = @UserId;

    IF @UserOnboardingId IS NULL
    BEGIN
        SELECT CAST(1 AS TINYINT) AS ResultCode;
        RETURN;
    END

    SELECT @StepId = [StepId], @CurrentSortOrder = [SortOrder]
    FROM   [MyMoney].[OnboardingSteps]
    WHERE  [StepKey] = @StepKey;

    IF @StepId IS NULL
    BEGIN
        SELECT CAST(2 AS TINYINT) AS ResultCode;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Upsert the step record
        IF EXISTS (
            SELECT 1 FROM [MyMoney].[UserOnboardingSteps]
            WHERE [UserOnboardingId] = @UserOnboardingId AND [StepId] = @StepId
        )
        BEGIN
            UPDATE [MyMoney].[UserOnboardingSteps]
            SET   [Status]         = @StepStatus,
                  [CompletedAtUtc] = GETUTCDATE()
            WHERE [UserOnboardingId] = @UserOnboardingId
              AND [StepId]           = @StepId;
        END
        ELSE
        BEGIN
            INSERT INTO [MyMoney].[UserOnboardingSteps]
                ([UserOnboardingId], [StepId], [Status], [StartedAtUtc], [CompletedAtUtc])
            VALUES
                (@UserOnboardingId, @StepId, @StepStatus, GETUTCDATE(), GETUTCDATE());
        END

        -- Find next pending step (after the current one in sort order)
        DECLARE @NextStepKey NVARCHAR(50) = NULL;

        SELECT TOP 1 @NextStepKey = os.[StepKey]
        FROM         [MyMoney].[OnboardingSteps]     os
        LEFT JOIN    [MyMoney].[UserOnboardingSteps] uos
            ON  uos.[UserOnboardingId] = @UserOnboardingId
            AND uos.[StepId]           = os.[StepId]
        WHERE  os.[SortOrder] > @CurrentSortOrder
          AND  (uos.[Status] IS NULL OR uos.[Status] IN (0, 1))  -- Pending or InProgress
        ORDER BY os.[SortOrder];

        IF @NextStepKey IS NULL
        BEGIN
            -- All steps done — mark onboarding as Completed
            UPDATE [MyMoney].[UserOnboarding]
            SET   [Status]           = 1,
                  [CurrentStepKey]   = @StepKey,
                  [LastUpdatedAtUtc] = GETUTCDATE(),
                  [CompletedAtUtc]   = GETUTCDATE()
            WHERE [UserOnboardingId] = @UserOnboardingId;

            UPDATE [MyMoney].[Users]
            SET   [OnboardingCompletedAtUtc] = GETUTCDATE()
            WHERE [UserId] = @UserId;
        END
        ELSE
        BEGIN
            -- Advance to the next step
            UPDATE [MyMoney].[UserOnboarding]
            SET   [CurrentStepKey]   = @NextStepKey,
                  [LastUpdatedAtUtc] = GETUTCDATE()
            WHERE [UserOnboardingId] = @UserOnboardingId;

            -- Insert next step as InProgress if not already recorded
            IF NOT EXISTS (
                SELECT 1 FROM [MyMoney].[UserOnboardingSteps] uos
                INNER JOIN [MyMoney].[OnboardingSteps] os ON os.[StepId] = uos.[StepId]
                WHERE uos.[UserOnboardingId] = @UserOnboardingId
                  AND os.[StepKey]           = @NextStepKey
            )
            BEGIN
                INSERT INTO [MyMoney].[UserOnboardingSteps]
                    ([UserOnboardingId], [StepId], [Status], [StartedAtUtc])
                SELECT @UserOnboardingId, [StepId], 1, GETUTCDATE()
                FROM   [MyMoney].[OnboardingSteps]
                WHERE  [StepKey] = @NextStepKey;
            END
        END

        COMMIT TRANSACTION;
        SELECT CAST(0 AS TINYINT) AS ResultCode;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- =============================================================================
-- 10. usp_Onboarding_Skip
-- =============================================================================
-- ============================================================
-- MyMoney.usp_Onboarding_Skip
-- Skips all remaining steps, marks the onboarding as Skipped,
-- and stamps Users.OnboardingCompletedAtUtc so subsequent
-- logins bypass the onboarding tour entirely.
--
-- ResultCode:
--     0 = Success
--     1 = UserOnboarding row not found
-- ============================================================
CREATE PROCEDURE [MyMoney].[usp_Onboarding_Skip]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserOnboardingId BIGINT;

    SELECT @UserOnboardingId = [UserOnboardingId]
    FROM   [MyMoney].[UserOnboarding]
    WHERE  [UserId] = @UserId;

    IF @UserOnboardingId IS NULL
    BEGIN
        SELECT CAST(1 AS TINYINT) AS ResultCode;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Mark all existing pending / in-progress steps as Skipped
        UPDATE [MyMoney].[UserOnboardingSteps]
        SET   [Status]         = 3,
              [CompletedAtUtc] = GETUTCDATE()
        WHERE [UserOnboardingId] = @UserOnboardingId
          AND [Status]           IN (0, 1);

        -- Insert Skipped rows for steps that never had a UserOnboardingSteps record
        INSERT INTO [MyMoney].[UserOnboardingSteps]
            ([UserOnboardingId], [StepId], [Status], [StartedAtUtc], [CompletedAtUtc])
        SELECT @UserOnboardingId, os.[StepId], 3, GETUTCDATE(), GETUTCDATE()
        FROM   [MyMoney].[OnboardingSteps] os
        WHERE  NOT EXISTS (
            SELECT 1 FROM [MyMoney].[UserOnboardingSteps] uos
            WHERE uos.[UserOnboardingId] = @UserOnboardingId
              AND uos.[StepId]           = os.[StepId]
        );

        -- Mark UserOnboarding as Skipped
        UPDATE [MyMoney].[UserOnboarding]
        SET   [Status]           = 3,
              [LastUpdatedAtUtc] = GETUTCDATE(),
              [CompletedAtUtc]   = GETUTCDATE()
        WHERE [UserOnboardingId] = @UserOnboardingId;

        -- Stamp Users so future logins bypass the onboarding tour
        UPDATE [MyMoney].[Users]
        SET   [OnboardingCompletedAtUtc] = GETUTCDATE()
        WHERE [UserId] = @UserId;

        COMMIT TRANSACTION;
        SELECT CAST(0 AS TINYINT) AS ResultCode;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
