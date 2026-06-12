-- =============================================================================
-- MyMoney.usp_Authentication_SaveRefreshToken
-- Persists a hashed refresh token for the given user.
-- Token column holds the SHA-256 hex hash; the raw token is never stored.
-- =============================================================================

CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_SaveRefreshToken
    @UserId         BIGINT,
    @Token          NVARCHAR(512),
    @ExpiresOnUtc   DATETIME2(0),
    @CreatedByIp    NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO MyMoney.RefreshTokens (UserId, Token, ExpiresOnUtc, CreatedByIp)
    VALUES (@UserId, @Token, @ExpiresOnUtc, @CreatedByIp);
END
GO
