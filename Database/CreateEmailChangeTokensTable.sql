-- =============================================================================
-- TABLE: MyMoney.EmailChangeTokens
-- Stores pending email change requests. One active request per user.
-- Token hash stored only (SHA-256) — raw token is sent to the user's new email.
-- =============================================================================

CREATE TABLE MyMoney.EmailChangeTokens
(
    Id          BIGINT          IDENTITY(1,1)   NOT NULL,
    UserId      BIGINT                          NOT NULL,
    NewEmail    NVARCHAR(254)                   NOT NULL,
    TokenHash   NVARCHAR(64)                    NOT NULL,
    ExpiresAtUtc DATETIME2(0)                  NOT NULL,
    CreatedAt   DATETIME2(0)                    NOT NULL    DEFAULT GETUTCDATE(),
    CreatedByIp NVARCHAR(45)                    NULL,
    UsedOnUtc   DATETIME2(0)                    NULL,
    UsedByIp    NVARCHAR(45)                    NULL,

    CONSTRAINT PK_EmailChangeTokens         PRIMARY KEY (Id),
    CONSTRAINT FK_EmailChangeTokens_Users   FOREIGN KEY (UserId) REFERENCES MyMoney.Users(UserId),
    CONSTRAINT UQ_EmailChangeTokens_TokenHash UNIQUE (TokenHash)
);
GO

CREATE INDEX IX_EmailChangeTokens_UserId ON MyMoney.EmailChangeTokens(UserId);
GO
