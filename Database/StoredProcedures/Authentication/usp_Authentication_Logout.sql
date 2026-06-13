-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_Logout
-- Description: Revokes a specific refresh token (logout).
--              Identified by token hash — no JWT required.
--              Idempotent: succeeds even if token is already revoked or not found.
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_Logout
    @TokenHash   NVARCHAR(64),
    @RevokedByIp NVARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE MyMoney.RefreshTokens
    SET    RevokedOnUtc  = GETUTCDATE(),
           RevokedByIp   = @RevokedByIp,
           ReasonRevoked = 'Logout'
    WHERE  Token        = @TokenHash
      AND  RevokedOnUtc IS NULL;
END;
GO
