-- ============================================================
-- Table: MyMoney.EmailConfirmationTokens
-- Description: Stores hashed email confirmation tokens.
--              Raw token is sent to user; only SHA-256 hash is persisted (Rule 17).
-- ============================================================
CREATE TABLE MyMoney.EmailConfirmationTokens
(
    TokenId        BIGINT          NOT NULL IDENTITY(1, 1),
    UserId         BIGINT          NOT NULL,
    TokenHash      NVARCHAR(64)    NOT NULL,   -- SHA-256 hex digest, always 64 chars
    ExpiresAtUtc   DATETIME2(7)    NOT NULL,
    CreatedAtUtc   DATETIME2(7)    NOT NULL CONSTRAINT DF_EmailConfirmationTokens_CreatedAtUtc DEFAULT GETUTCDATE(),
    ConfirmedAtUtc DATETIME2(7)    NULL,
    IsUsed         BIT             NOT NULL CONSTRAINT DF_EmailConfirmationTokens_IsUsed DEFAULT 0,
    CreatedByIp    NVARCHAR(45)    NULL,
    UsedByIp       NVARCHAR(45)    NULL,

    CONSTRAINT PK_EmailConfirmationTokens
        PRIMARY KEY CLUSTERED (TokenId),

    CONSTRAINT FK_EmailConfirmationTokens_Users
        FOREIGN KEY (UserId) REFERENCES MyMoney.Users(UserId)
);
GO

-- Fast lookup by hash (used by ConfirmEmail SP)
CREATE UNIQUE INDEX UX_EmailConfirmationTokens_TokenHash
    ON MyMoney.EmailConfirmationTokens (TokenHash);
GO

-- Supports finding pending tokens per user (used by SaveConfirmationToken SP)
CREATE INDEX IX_EmailConfirmationTokens_UserId_IsUsed
    ON MyMoney.EmailConfirmationTokens (UserId, IsUsed);
GO
