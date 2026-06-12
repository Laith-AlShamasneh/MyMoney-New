-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_ConfirmEmail
-- Description: Validates a confirmation token and atomically marks the user's
--              email as confirmed.
--
-- ResultCode values returned as a single-row SELECT:
--   0 = Success
--   1 = NotFound  (token does not exist or user is deleted)
--   2 = Expired   (token has passed ExpiresAtUtc)
--   3 = AlreadyUsed (token was previously consumed)
--   4 = AlreadyConfirmed (email was already confirmed — idempotent, treated as success)
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_ConfirmEmail
    @TokenHash NVARCHAR(64),
    @UsedByIp  NVARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TokenId          BIGINT;
    DECLARE @UserId           BIGINT;
    DECLARE @ExpiresAtUtc     DATETIME2(7);
    DECLARE @IsUsed           BIT;
    DECLARE @IsEmailConfirmed BIT;

    BEGIN TRANSACTION;

    -- Lock the row to prevent concurrent confirmation of the same token
    SELECT
        @TokenId          = t.TokenId,
        @UserId           = t.UserId,
        @ExpiresAtUtc     = t.ExpiresAtUtc,
        @IsUsed           = t.IsUsed,
        @IsEmailConfirmed = u.IsEmailConfirmed
    FROM   MyMoney.EmailConfirmationTokens t WITH (UPDLOCK, ROWLOCK)
    INNER JOIN MyMoney.Users u ON u.UserId = t.UserId AND u.IsDeleted = 0
    WHERE  t.TokenHash = @TokenHash;

    -- Token not found or user deleted
    IF @TokenId IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT CAST(1 AS TINYINT) AS ResultCode;
        RETURN;
    END

    -- Email already confirmed — idempotent success
    IF @IsEmailConfirmed = 1
    BEGIN
        COMMIT TRANSACTION;
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

    -- All checks passed: mark token used and confirm the user's email
    UPDATE MyMoney.EmailConfirmationTokens
    SET    IsUsed         = 1,
           ConfirmedAtUtc = GETUTCDATE(),
           UsedByIp       = @UsedByIp
    WHERE  TokenId = @TokenId;

    UPDATE MyMoney.Users
    SET    IsEmailConfirmed = 1
    WHERE  UserId = @UserId;

    COMMIT TRANSACTION;
    SELECT CAST(0 AS TINYINT) AS ResultCode;
END;
GO
