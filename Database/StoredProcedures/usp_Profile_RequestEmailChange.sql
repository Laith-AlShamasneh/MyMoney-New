-- ============================================================
-- Stored Procedure: MyMoney.usp_Profile_RequestEmailChange
-- Description: Cancels any existing pending email change for the user and
--              inserts the new request. One active request per user at a time.
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Profile_RequestEmailChange
    @UserId       BIGINT,
    @NewEmail     NVARCHAR(254),
    @TokenHash    NVARCHAR(64),
    @ExpiresAtUtc DATETIME2(0),
    @CreatedByIp  NVARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;

    -- Cancel any previously pending (unused, non-expired) request for this user
    DELETE FROM MyMoney.EmailChangeTokens
    WHERE  UserId    = @UserId
      AND  UsedOnUtc IS NULL;

    -- Insert the new request
    INSERT INTO MyMoney.EmailChangeTokens
        (UserId, NewEmail, TokenHash, ExpiresAtUtc, CreatedByIp)
    VALUES
        (@UserId, @NewEmail, @TokenHash, @ExpiresAtUtc, @CreatedByIp);

    COMMIT TRANSACTION;
END;
GO
