-- ============================================================
-- Stored Procedure: MyMoney.usp_Profile_RevokeSession
-- Description: Revokes a specific refresh token that belongs to the user.
--
-- ResultCode values:
--   0 = Success
--   1 = NotFound or does not belong to user
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Profile_RevokeSession
    @UserId      BIGINT,
    @TokenId     BIGINT,
    @RevokedByIp NVARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE MyMoney.RefreshTokens
    SET    RevokedOnUtc  = GETUTCDATE(),
           RevokedByIp   = @RevokedByIp,
           ReasonRevoked = 'UserRevoked'
    WHERE  Id          = @TokenId
      AND  UserId      = @UserId
      AND  RevokedOnUtc IS NULL
      AND  ExpiresOnUtc > GETUTCDATE();

    IF @@ROWCOUNT = 0
        SELECT CAST(1 AS TINYINT) AS ResultCode;
    ELSE
        SELECT CAST(0 AS TINYINT) AS ResultCode;
END;
GO
