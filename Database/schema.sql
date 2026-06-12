-- =============================================================================
-- MyMoney — Complete Database Schema
-- SQL Server 2019+
-- Generated: 2026-06-12
-- =============================================================================

-- =============================================================================
-- SCHEMA
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'MyMoney')
    EXEC sp_executesql N'CREATE SCHEMA MyMoney';
GO

-- =============================================================================
-- TABLE: MyMoney.Persons
-- Stores the physical person's identity and personal information.
-- Decoupled from auth so one person can theoretically have multiple accounts
-- (or future SSO/federation scenarios).
-- =============================================================================

CREATE TABLE MyMoney.Persons
(
    PersonId        BIGINT          IDENTITY(1,1)   NOT NULL,

    -- Names (bilingual)
    FirstNameEn     NVARCHAR(100)                   NOT NULL,
    LastNameEn      NVARCHAR(100)                   NOT NULL,
    FirstNameAr     NVARCHAR(100)                   NULL,
    LastNameAr      NVARCHAR(100)                   NULL,
    DisplayNameEn   NVARCHAR(200)                   NOT NULL,
    DisplayNameAr   NVARCHAR(200)                   NULL,

    -- Personal details
    DateOfBirth     DATE                            NULL,
    GenderId        TINYINT                         NULL,   -- 1=Male 2=Female 3=PreferNotToSay
    ProfilePicture  NVARCHAR(500)                   NULL,   -- file name only; URL built at runtime

    -- BaseEntity
    IsActive        BIT                             NOT NULL    DEFAULT 1,
    CreatedAt       DATETIME2(0)                    NOT NULL    DEFAULT GETUTCDATE(),
    CreatedBy       BIGINT                          NULL,
    UpdatedAt       DATETIME2(0)                    NULL,
    UpdatedBy       BIGINT                          NULL,

    CONSTRAINT PK_Persons               PRIMARY KEY (PersonId),
    CONSTRAINT CK_Persons_GenderId      CHECK (GenderId IN (1, 2, 3))
);
GO

-- =============================================================================
-- TABLE: MyMoney.Roles
-- Application roles. Seeded with system roles (IsSystemRole = 1).
-- =============================================================================

CREATE TABLE MyMoney.Roles
(
    RoleId          INT             IDENTITY(1,1)   NOT NULL,
    NameEn          NVARCHAR(100)                   NOT NULL,
    NameAr          NVARCHAR(100)                   NOT NULL,
    Description     NVARCHAR(500)                   NULL,
    IsSystemRole    BIT                             NOT NULL    DEFAULT 0,

    -- BaseEntity
    IsActive        BIT                             NOT NULL    DEFAULT 1,
    CreatedAt       DATETIME2(0)                    NOT NULL    DEFAULT GETUTCDATE(),
    CreatedBy       BIGINT                          NULL,
    UpdatedAt       DATETIME2(0)                    NULL,
    UpdatedBy       BIGINT                          NULL,

    CONSTRAINT PK_Roles           PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Roles_NameEn    UNIQUE      (NameEn)
);
GO

-- =============================================================================
-- TABLE: MyMoney.Users
-- Authentication account. Linked 1-to-1 with a Person.
-- Stores only auth/security state — no personal data lives here.
-- =============================================================================

CREATE TABLE MyMoney.Users
(
    UserId                  BIGINT          IDENTITY(1,1)   NOT NULL,
    PersonId                BIGINT                          NOT NULL,

    -- Credentials
    Email                   NVARCHAR(256)                   NOT NULL,
    PasswordHash            NVARCHAR(512)                   NOT NULL,

    -- Account state
    IsEmailConfirmed        BIT                             NOT NULL    DEFAULT 0,
    IsLocked                BIT                             NOT NULL    DEFAULT 0,
    FailedLoginAttempts     INT                             NOT NULL    DEFAULT 0,
    LastLoginDateUtc        DATETIME2(0)                    NULL,
    LockoutEndDateUtc       DATETIME2(0)                    NULL,

    -- BaseEntity
    IsActive                BIT                             NOT NULL    DEFAULT 1,
    CreatedAt               DATETIME2(0)                    NOT NULL    DEFAULT GETUTCDATE(),
    CreatedBy               BIGINT                          NULL,
    UpdatedAt               DATETIME2(0)                    NULL,
    UpdatedBy               BIGINT                          NULL,

    CONSTRAINT PK_Users             PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_Email       UNIQUE      (Email),
    CONSTRAINT UQ_Users_PersonId    UNIQUE      (PersonId),     -- one account per person
    CONSTRAINT FK_Users_Persons     FOREIGN KEY (PersonId) REFERENCES MyMoney.Persons(PersonId),
    CONSTRAINT CK_Users_FailedLogin CHECK (FailedLoginAttempts >= 0)
);
GO

-- =============================================================================
-- TABLE: MyMoney.UserRoles
-- Many-to-many junction: a user can hold multiple roles.
-- =============================================================================

CREATE TABLE MyMoney.UserRoles
(
    UserId      BIGINT  NOT NULL,
    RoleId      INT     NOT NULL,
    CreatedAt   DATETIME2(0)    NOT NULL    DEFAULT GETUTCDATE(),
    CreatedBy   BIGINT          NULL,

    CONSTRAINT PK_UserRoles         PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users   FOREIGN KEY (UserId)  REFERENCES MyMoney.Users(UserId),
    CONSTRAINT FK_UserRoles_Roles   FOREIGN KEY (RoleId)  REFERENCES MyMoney.Roles(RoleId)
);
GO

-- =============================================================================
-- TABLE: MyMoney.RefreshTokens
-- Opaque refresh tokens for JWT rotation.
-- Token column stores a SHA-256 hex hash of the raw token sent to the client.
-- The raw token is never stored — only the hash is persisted for verification.
-- Supports full security audit: creation IP, revocation IP, revocation reason,
-- and the replacement token reference for rotation chains.
-- =============================================================================

CREATE TABLE MyMoney.RefreshTokens
(
    TokenId             BIGINT          IDENTITY(1,1)   NOT NULL,
    UserId              BIGINT                          NOT NULL,

    -- Token (SHA-256 hex of the raw opaque random string sent to client)
    Token               NVARCHAR(512)                   NOT NULL,
    ExpiresOnUtc        DATETIME2(0)                    NOT NULL,
    CreatedOnUtc        DATETIME2(0)                    NOT NULL    DEFAULT GETUTCDATE(),
    CreatedByIp         NVARCHAR(50)                    NULL,

    -- Revocation
    RevokedOnUtc        DATETIME2(0)                    NULL,
    RevokedByIp         NVARCHAR(50)                    NULL,
    ReasonRevoked       NVARCHAR(500)                   NULL,

    -- Token rotation: points to the replacement token after rotation
    ReplacedByToken     NVARCHAR(512)                   NULL,

    CONSTRAINT PK_RefreshTokens         PRIMARY KEY (TokenId),
    CONSTRAINT UQ_RefreshTokens_Token   UNIQUE      (Token),
    CONSTRAINT FK_RefreshTokens_Users   FOREIGN KEY (UserId) REFERENCES MyMoney.Users(UserId)
);
GO

-- =============================================================================
-- TABLE: MyMoney.Categories
-- System-defined only — no user-created categories.
-- Seeded at database creation time.
-- TransactionTypeId: 1 = Income, 2 = Expense
-- =============================================================================

CREATE TABLE MyMoney.Categories
(
    CategoryId          INT             IDENTITY(1,1)   NOT NULL,
    NameEn              NVARCHAR(100)                   NOT NULL,
    NameAr              NVARCHAR(100)                   NOT NULL,
    TransactionTypeId   TINYINT                         NOT NULL,
    IconFileName        NVARCHAR(500)                   NULL,
    SortOrder           SMALLINT                        NOT NULL    DEFAULT 0,

    -- BaseEntity
    IsActive            BIT                             NOT NULL    DEFAULT 1,
    CreatedAt           DATETIME2(0)                    NOT NULL    DEFAULT GETUTCDATE(),
    CreatedBy           BIGINT                          NULL,
    UpdatedAt           DATETIME2(0)                    NULL,
    UpdatedBy           BIGINT                          NULL,

    CONSTRAINT PK_Categories                    PRIMARY KEY (CategoryId),
    CONSTRAINT CK_Categories_TransactionTypeId  CHECK (TransactionTypeId IN (1, 2))
);
GO

-- =============================================================================
-- TABLE: MyMoney.Transactions
-- Core financial ledger. One row per financial event.
-- TransactionTypeId is denormalized (copied from category at insert time) to
-- allow efficient dashboard aggregations without a category join.
-- Amount is always stored as a positive decimal; the sign is inferred from
-- TransactionTypeId at the application layer.
-- =============================================================================

CREATE TABLE MyMoney.Transactions
(
    TransactionId       BIGINT          IDENTITY(1,1)   NOT NULL,
    UserId              BIGINT                          NOT NULL,
    CategoryId          INT                             NOT NULL,
    TransactionTypeId   TINYINT                         NOT NULL,   -- denormalized
    Amount              DECIMAL(18, 2)                  NOT NULL,
    Description         NVARCHAR(500)                   NULL,
    TransactionDate     DATE                            NOT NULL,
    Notes               NVARCHAR(1000)                  NULL,

    -- BaseEntity
    IsActive            BIT                             NOT NULL    DEFAULT 1,
    CreatedAt           DATETIME2(0)                    NOT NULL    DEFAULT GETUTCDATE(),
    CreatedBy           BIGINT                          NULL,
    UpdatedAt           DATETIME2(0)                    NULL,
    UpdatedBy           BIGINT                          NULL,

    CONSTRAINT PK_Transactions              PRIMARY KEY (TransactionId),
    CONSTRAINT FK_Transactions_Users        FOREIGN KEY (UserId)     REFERENCES MyMoney.Users(UserId),
    CONSTRAINT FK_Transactions_Categories   FOREIGN KEY (CategoryId) REFERENCES MyMoney.Categories(CategoryId),
    CONSTRAINT CK_Transactions_Amount       CHECK (Amount > 0),
    CONSTRAINT CK_Transactions_TypeId       CHECK (TransactionTypeId IN (1, 2))
);
GO

-- =============================================================================
-- TABLE: MyMoney.BackgroundJobs
-- Generic outbox/job-queue table. All async background work is enqueued here.
-- A hosted service polls this table to pick up and process jobs.
--
-- StatusId: 1=Pending  2=Processing  3=Completed  4=Failed  5=Cancelled
-- Priority: 1=High     2=Normal      3=Low
--
-- Job types (examples):
--   PasswordResetEmail   — send password reset link
--   WelcomeEmail         — send registration welcome
--   MonthlyReport        — generate + send monthly summary
--   PushNotification     — future mobile/web push
-- =============================================================================

CREATE TABLE MyMoney.BackgroundJobs
(
    JobId               BIGINT          IDENTITY(1,1)   NOT NULL,
    JobType             NVARCHAR(200)                   NOT NULL,
    Payload             NVARCHAR(MAX)                   NOT NULL,   -- JSON
    StatusId            TINYINT                         NOT NULL    DEFAULT 1,
    Priority            TINYINT                         NOT NULL    DEFAULT 2,

    -- Scheduling
    ScheduledAt         DATETIME2(0)                    NOT NULL    DEFAULT GETUTCDATE(),
    PickedUpAt          DATETIME2(0)                    NULL,
    CompletedAt         DATETIME2(0)                    NULL,

    -- Retry
    AttemptCount        INT                             NOT NULL    DEFAULT 0,
    MaxAttempts         INT                             NOT NULL    DEFAULT 3,
    LastAttemptAt       DATETIME2(0)                    NULL,
    NextRetryAt         DATETIME2(0)                    NULL,
    ErrorMessage        NVARCHAR(MAX)                   NULL,

    -- Audit
    CreatedAt           DATETIME2(0)                    NOT NULL    DEFAULT GETUTCDATE(),
    CreatedBy           BIGINT                          NULL,

    CONSTRAINT PK_BackgroundJobs                PRIMARY KEY (JobId),
    CONSTRAINT CK_BackgroundJobs_StatusId       CHECK (StatusId  IN (1, 2, 3, 4, 5)),
    CONSTRAINT CK_BackgroundJobs_Priority       CHECK (Priority  IN (1, 2, 3)),
    CONSTRAINT CK_BackgroundJobs_MaxAttempts    CHECK (MaxAttempts  >= 1),
    CONSTRAINT CK_BackgroundJobs_AttemptCount   CHECK (AttemptCount >= 0)
);
GO

-- =============================================================================
-- INDEXES
-- =============================================================================

-- Users
CREATE INDEX IX_Users_PersonId
    ON MyMoney.Users (PersonId);

-- Transactions — primary query pattern: per-user ordered by date
CREATE INDEX IX_Transactions_UserId_Date
    ON MyMoney.Transactions (UserId, TransactionDate DESC)
    INCLUDE (TransactionTypeId, Amount, CategoryId, Description, IsActive);

-- Transactions — dashboard aggregation: per-user per type
CREATE INDEX IX_Transactions_UserId_TypeId
    ON MyMoney.Transactions (UserId, TransactionTypeId)
    INCLUDE (Amount, TransactionDate, IsActive);

-- Transactions — filter by category
CREATE INDEX IX_Transactions_UserId_CategoryId
    ON MyMoney.Transactions (UserId, CategoryId)
    INCLUDE (TransactionDate, Amount, TransactionTypeId, IsActive);

-- Transactions — filter / sort by amount
CREATE INDEX IX_Transactions_UserId_Amount
    ON MyMoney.Transactions (UserId, Amount)
    INCLUDE (TransactionDate, TransactionTypeId, CategoryId, IsActive);

-- RefreshTokens — revocation check by userId (list active tokens)
CREATE INDEX IX_RefreshTokens_UserId
    ON MyMoney.RefreshTokens (UserId)
    INCLUDE (Token, ExpiresOnUtc, RevokedOnUtc);

-- BackgroundJobs — job processor polling: pending/failed jobs due for execution
CREATE INDEX IX_BackgroundJobs_Status_Scheduled
    ON MyMoney.BackgroundJobs (StatusId, ScheduledAt)
    INCLUDE (JobType, Priority, AttemptCount, MaxAttempts, NextRetryAt)
    WHERE StatusId IN (1, 4);   -- Pending or Failed (eligible for pickup)

-- BackgroundJobs — retry: find failed jobs ready to retry
CREATE INDEX IX_BackgroundJobs_NextRetryAt
    ON MyMoney.BackgroundJobs (NextRetryAt)
    INCLUDE (StatusId, AttemptCount, MaxAttempts)
    WHERE StatusId = 4 AND NextRetryAt IS NOT NULL;

GO

-- =============================================================================
-- SEED DATA
-- =============================================================================

-- Roles
INSERT INTO MyMoney.Roles (NameEn, NameAr, Description, IsSystemRole, IsActive, CreatedAt)
VALUES
    ('Admin', N'مدير',     'Full system access. Can manage users and reference data.', 1, 1, GETUTCDATE()),
    ('User',  N'مستخدم',   'Standard user. Can manage their own financial data.',      1, 1, GETUTCDATE());
GO

-- Categories — Income (TransactionTypeId = 1)
INSERT INTO MyMoney.Categories (NameEn, NameAr, TransactionTypeId, SortOrder, IsActive, CreatedAt)
VALUES
    ('Salary',          N'الراتب',              1, 1,  1, GETUTCDATE()),
    ('Freelance',       N'عمل حر',              1, 2,  1, GETUTCDATE()),
    ('Investment',      N'استثمار',             1, 3,  1, GETUTCDATE()),
    ('Bonus',           N'مكافأة',              1, 4,  1, GETUTCDATE()),
    ('Business',        N'أعمال تجارية',        1, 5,  1, GETUTCDATE()),
    ('Rental Income',   N'دخل إيجار',           1, 6,  1, GETUTCDATE()),
    ('Gift',            N'هدية',                1, 7,  1, GETUTCDATE()),
    ('Other Income',    N'دخل آخر',             1, 99, 1, GETUTCDATE());
GO

-- Categories — Expense (TransactionTypeId = 2)
INSERT INTO MyMoney.Categories (NameEn, NameAr, TransactionTypeId, SortOrder, IsActive, CreatedAt)
VALUES
    ('Food & Dining',       N'الطعام والمطاعم',     2, 1,  1, GETUTCDATE()),
    ('Transportation',      N'المواصلات',            2, 2,  1, GETUTCDATE()),
    ('Shopping',            N'التسوق',               2, 3,  1, GETUTCDATE()),
    ('Bills & Utilities',   N'الفواتير والمرافق',   2, 4,  1, GETUTCDATE()),
    ('Entertainment',       N'الترفيه',              2, 5,  1, GETUTCDATE()),
    ('Healthcare',          N'الرعاية الصحية',       2, 6,  1, GETUTCDATE()),
    ('Education',           N'التعليم',              2, 7,  1, GETUTCDATE()),
    ('Housing & Rent',      N'السكن والإيجار',       2, 8,  1, GETUTCDATE()),
    ('Personal Care',       N'العناية الشخصية',      2, 9,  1, GETUTCDATE()),
    ('Travel',              N'السفر',                2, 10, 1, GETUTCDATE()),
    ('Subscriptions',       N'الاشتراكات',           2, 11, 1, GETUTCDATE()),
    ('Other Expense',       N'مصاريف أخرى',          2, 99, 1, GETUTCDATE());
GO
