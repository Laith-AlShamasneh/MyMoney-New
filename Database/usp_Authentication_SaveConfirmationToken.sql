-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_SaveConfirmationToken
-- Description: Invalidates all previous pending confirmation tokens for the
--              given user, then inserts a new one.
--              Called on both Register and Resend-Confirmation flows.
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_SaveConfirmationToken
    @UserId       BIGINT,
    @TokenHash    NVARCHAR(64),
    @ExpiresAtUtc DATETIME2(7),
    @CreatedByIp  NVARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;

    -- Invalidate all existing pending tokens for this user
    UPDATE MyMoney.EmailConfirmationTokens
    SET    IsUsed = 1
    WHERE  UserId = @UserId
      AND  IsUsed = 0;

    -- Insert new token
    INSERT INTO MyMoney.EmailConfirmationTokens
        (UserId, TokenHash, ExpiresAtUtc, CreatedByIp)
    VALUES
        (@UserId, @TokenHash, @ExpiresAtUtc, @CreatedByIp);
END;
GO
