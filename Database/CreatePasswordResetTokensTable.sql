-- ============================================================
-- Table: MyMoney.PasswordResetTokens
-- Description: Stores hashed password reset tokens.
--              Raw token is sent to user; only SHA-256 hash is persisted (Rule 17).
--              Tokens are short-lived (default 15 min), single-use, and
--              invalidated on use or when a new reset is requested.
-- ============================================================
CREATE TABLE MyMoney.PasswordResetTokens
(
    TokenId       BIGINT          NOT NULL IDENTITY(1, 1),
    UserId        BIGINT          NOT NULL,
    TokenHash     NVARCHAR(64)    NOT NULL,   -- SHA-256 hex digest, always 64 chars
    ExpiresAtUtc  DATETIME2(7)    NOT NULL,
    CreatedAtUtc  DATETIME2(7)    NOT NULL CONSTRAINT DF_PasswordResetTokens_CreatedAtUtc DEFAULT GETUTCDATE(),
    ResetAtUtc    DATETIME2(7)    NULL,
    IsUsed        BIT             NOT NULL CONSTRAINT DF_PasswordResetTokens_IsUsed       DEFAULT 0,
    CreatedByIp   NVARCHAR(45)    NULL,
    UsedByIp      NVARCHAR(45)    NULL,

    CONSTRAINT PK_PasswordResetTokens
        PRIMARY KEY CLUSTERED (TokenId),

    CONSTRAINT FK_PasswordResetTokens_Users
        FOREIGN KEY (UserId) REFERENCES MyMoney.Users(UserId)
);
GO

-- Fast lookup by hash (used by ValidatePasswordResetToken and ResetPassword SPs)
CREATE UNIQUE INDEX UX_PasswordResetTokens_TokenHash
    ON MyMoney.PasswordResetTokens (TokenHash);
GO

-- Supports finding pending tokens per user (used by SavePasswordResetToken SP)
CREATE INDEX IX_PasswordResetTokens_UserId_IsUsed
    ON MyMoney.PasswordResetTokens (UserId, IsUsed);
GO
