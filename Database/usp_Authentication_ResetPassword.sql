-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_ResetPassword
-- Description: Atomically validates the reset token, updates the user's
--              password hash, marks the token as used, and revokes all
--              active refresh tokens — forcing re-login on all devices.
--
-- ResultCode values:
--   0 = Success
--   1 = NotFound  (token does not exist or user is deleted)
--   2 = Expired
--   3 = AlreadyUsed
--   4 = UserInactive
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_ResetPassword
    @TokenHash    NVARCHAR(64),
    @PasswordHash NVARCHAR(512),
    @UsedByIp     NVARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TokenId      BIGINT;
    DECLARE @UserId       BIGINT;
    DECLARE @ExpiresAtUtc DATETIME2(7);
    DECLARE @IsUsed       BIT;
    DECLARE @IsActive     BIT;

    BEGIN TRANSACTION;

    -- Lock the token row to prevent concurrent resets with the same token
    SELECT
        @TokenId      = t.TokenId,
        @UserId       = t.UserId,
        @ExpiresAtUtc = t.ExpiresAtUtc,
        @IsUsed       = t.IsUsed,
        @IsActive     = u.IsActive
    FROM   MyMoney.PasswordResetTokens t WITH (UPDLOCK, ROWLOCK)
    INNER JOIN MyMoney.Users u ON u.UserId = t.UserId AND u.IsDeleted = 0
    WHERE  t.TokenHash = @TokenHash;

    -- Token not found or user deleted
    IF @TokenId IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT CAST(1 AS TINYINT) AS ResultCode;
        RETURN;
    END

    -- User account is inactive
    IF @IsActive = 0
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT CAST(4 AS TINYINT) AS ResultCode;
        RETURN;
    END

    -- Token was already used (replay attack)
    IF @IsUsed = 1
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT CAST(3 AS TINYINT) AS ResultCode;
        RETURN;
    END

    -- Token has expired
    IF @ExpiresAtUtc < GETUTCDATE()
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT CAST(2 AS TINYINT) AS ResultCode;
        RETURN;
    END

    -- 1. Update the user's password
    UPDATE MyMoney.Users
    SET    PasswordHash            = @PasswordHash,
           FailedLoginAttempts     = 0,
           IsLocked                = 0,
           LockoutEndDateUtc       = NULL
    WHERE  UserId = @UserId;

    -- 2. Mark the reset token as used
    UPDATE MyMoney.PasswordResetTokens
    SET    IsUsed    = 1,
           ResetAtUtc = GETUTCDATE(),
           UsedByIp  = @UsedByIp
    WHERE  TokenId = @TokenId;

    -- 3. Revoke all active refresh tokens — forces re-login on all devices
    UPDATE MyMoney.RefreshTokens
    SET    RevokedOnUtc  = GETUTCDATE(),
           RevokedByIp   = @UsedByIp,
           ReasonRevoked = 'PasswordReset'
    WHERE  UserId       = @UserId
      AND  RevokedOnUtc IS NULL
      AND  ExpiresOnUtc > GETUTCDATE();

    COMMIT TRANSACTION;
    SELECT CAST(0 AS TINYINT) AS ResultCode;
END;
GO
