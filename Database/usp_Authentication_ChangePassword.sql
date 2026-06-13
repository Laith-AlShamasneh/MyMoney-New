-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_ChangePassword
-- Description: Atomically updates the user's password hash and revokes active
--              refresh tokens. If @CurrentTokenHash is provided, the matching
--              session is kept alive (user stays logged in after password change).
--
-- The application layer is responsible for:
--   - Verifying the current password matches the stored hash
--   - Ensuring the new password differs from the current password
--   - Hashing the new password before calling this SP
--   - Hashing the current refresh token before passing as @CurrentTokenHash
--
-- ResultCode values:
--   0 = Success
--   1 = UserNotFoundOrInactive
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_ChangePassword
    @UserId           BIGINT,
    @NewPasswordHash  NVARCHAR(512),
    @ChangedByIp      NVARCHAR(45),
    @CurrentTokenHash NVARCHAR(64) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;

    IF NOT EXISTS (
        SELECT 1
        FROM   MyMoney.Users WITH (UPDLOCK, ROWLOCK)
        WHERE  UserId   = @UserId
          AND  IsActive = 1
    )
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT CAST(1 AS TINYINT) AS ResultCode;
        RETURN;
    END

    UPDATE MyMoney.Users
    SET    PasswordHash = @NewPasswordHash,
           UpdatedAt    = GETUTCDATE()
    WHERE  UserId = @UserId;

    -- Revoke all active refresh tokens except the current session (if provided)
    UPDATE MyMoney.RefreshTokens
    SET    RevokedOnUtc  = GETUTCDATE(),
           RevokedByIp   = @ChangedByIp,
           ReasonRevoked = 'PasswordChanged'
    WHERE  UserId       = @UserId
      AND  RevokedOnUtc IS NULL
      AND  ExpiresOnUtc > GETUTCDATE()
      AND  (@CurrentTokenHash IS NULL OR Token <> @CurrentTokenHash);

    COMMIT TRANSACTION;
    SELECT CAST(0 AS TINYINT) AS ResultCode;
END;
GO
