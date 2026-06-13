-- ============================================================
-- Stored Procedure: MyMoney.usp_Profile_GetSessions
-- Description: Lists all active (non-revoked, non-expired) refresh tokens
--              for a user. Marks the current session if @CurrentTokenHash provided.
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Profile_GetSessions
    @UserId           BIGINT,
    @CurrentTokenHash NVARCHAR(64) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        TokenId,
        CreatedByIp,
        CreatedOnUtc,
        ExpiresOnUtc,
        CASE WHEN @CurrentTokenHash IS NOT NULL AND Token = @CurrentTokenHash
             THEN CAST(1 AS BIT)
             ELSE CAST(0 AS BIT)
        END AS IsCurrentSession
    FROM   MyMoney.RefreshTokens
    WHERE  UserId       = @UserId
      AND  RevokedOnUtc IS NULL
      AND  ExpiresOnUtc > GETUTCDATE()
    ORDER BY CreatedOnUtc DESC;
END;
GO
