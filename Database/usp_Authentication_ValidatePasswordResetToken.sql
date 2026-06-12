-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_ValidatePasswordResetToken
-- Description: Validates a password reset token without consuming it.
--              Used by the validate-token endpoint so the frontend can
--              confirm the token is still valid before showing the
--              new-password form.
--
-- ResultCode values:
--   0 = Valid
--   1 = NotFound  (token does not exist or user is deleted)
--   2 = Expired
--   3 = AlreadyUsed
--   4 = UserInactive
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_ValidatePasswordResetToken
    @TokenHash NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserId       BIGINT;
    DECLARE @ExpiresAtUtc DATETIME2(7);
    DECLARE @IsUsed       BIT;
    DECLARE @IsActive     BIT;

    SELECT
        @UserId       = t.UserId,
        @ExpiresAtUtc = t.ExpiresAtUtc,
        @IsUsed       = t.IsUsed,
        @IsActive     = u.IsActive
    FROM   MyMoney.PasswordResetTokens t
    INNER JOIN MyMoney.Users u ON u.UserId = t.UserId AND u.IsDeleted = 0
    WHERE  t.TokenHash = @TokenHash;

    -- Token not found or user deleted
    IF @UserId IS NULL
    BEGIN
        SELECT CAST(1 AS TINYINT) AS ResultCode;
        RETURN;
    END

    -- User account is inactive
    IF @IsActive = 0
    BEGIN
        SELECT CAST(4 AS TINYINT) AS ResultCode;
        RETURN;
    END

    -- Token was already used
    IF @IsUsed = 1
    BEGIN
        SELECT CAST(3 AS TINYINT) AS ResultCode;
        RETURN;
    END

    -- Token has expired
    IF @ExpiresAtUtc < GETUTCDATE()
    BEGIN
        SELECT CAST(2 AS TINYINT) AS ResultCode;
        RETURN;
    END

    -- All checks passed
    SELECT CAST(0 AS TINYINT) AS ResultCode;
END;
GO
