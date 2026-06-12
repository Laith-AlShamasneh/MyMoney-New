-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_SavePasswordResetToken
-- Description: Invalidates all previous pending reset tokens for the given
--              user, then inserts a new one. Ensures only one active reset
--              token exists per user at any time (natural rate-limit: one
--              pending request per user).
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_SavePasswordResetToken
    @UserId       BIGINT,
    @TokenHash    NVARCHAR(64),
    @ExpiresAtUtc DATETIME2(7),
    @CreatedByIp  NVARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;

    -- Invalidate all existing pending reset tokens for this user
    UPDATE MyMoney.PasswordResetTokens
    SET    IsUsed = 1
    WHERE  UserId = @UserId
      AND  IsUsed = 0;

    -- Insert new reset token
    INSERT INTO MyMoney.PasswordResetTokens
        (UserId, TokenHash, ExpiresAtUtc, CreatedByIp)
    VALUES
        (@UserId, @TokenHash, @ExpiresAtUtc, @CreatedByIp);
END;
GO
