-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_ChangePassword
-- Description: Atomically updates the user's password hash and revokes all
--              active refresh tokens, forcing re-login on all other devices.
--
-- The application layer is responsible for:
--   - Verifying the current password matches the stored hash
--   - Ensuring the new password differs from the current password
--   - Hashing the new password before calling this SP
--
-- ResultCode values:
--   0 = Success
--   1 = UserNotFoundOrInactive
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_ChangePassword
    @UserId         BIGINT,
    @NewPasswordHash NVARCHAR(512),
    @ChangedByIp    NVARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;

    -- Verify the user still exists and is active under a lock
    IF NOT EXISTS (
        SELECT 1
        FROM   MyMoney.Users WITH (UPDLOCK, ROWLOCK)
        WHERE  UserId    = @UserId
          AND  IsActive  = 1
          AND  IsDeleted = 0
    )
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT CAST(1 AS TINYINT) AS ResultCode;
        RETURN;
    END

    -- Update password and audit timestamp
    UPDATE MyMoney.Users
    SET    PasswordHash = @NewPasswordHash,
           UpdatedAt    = GETUTCDATE()
    WHERE  UserId = @UserId;

    -- Revoke all active refresh tokens — forces re-login on all devices
    UPDATE MyMoney.RefreshTokens
    SET    RevokedOnUtc  = GETUTCDATE(),
           RevokedByIp   = @ChangedByIp,
           ReasonRevoked = 'PasswordChanged'
    WHERE  UserId       = @UserId
      AND  RevokedOnUtc IS NULL
      AND  ExpiresOnUtc > GETUTCDATE();

    COMMIT TRANSACTION;
    SELECT CAST(0 AS TINYINT) AS ResultCode;
END;
GO
