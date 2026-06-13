-- ============================================================
-- Stored Procedure: MyMoney.usp_Profile_RevokeAllOtherSessions
-- Description: Revokes all active refresh tokens for a user EXCEPT
--              the session identified by @CurrentTokenHash.
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Profile_RevokeAllOtherSessions
    @UserId           BIGINT,
    @CurrentTokenHash NVARCHAR(64),
    @RevokedByIp      NVARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE MyMoney.RefreshTokens
    SET    RevokedOnUtc  = GETUTCDATE(),
           RevokedByIp   = @RevokedByIp,
           ReasonRevoked = 'UserRevokedOtherSessions'
    WHERE  UserId       = @UserId
      AND  Token        <> @CurrentTokenHash
      AND  RevokedOnUtc IS NULL
      AND  ExpiresOnUtc > GETUTCDATE();
END;
GO
