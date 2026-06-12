-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_UpdateLogin
-- Description: Atomically updates login-related state on the Users row.
--
--   Success path (@LoginSucceeded = 1):
--     - Reset FailedLoginAttempts to 0
--     - Clear IsLocked and LockoutEndDateUtc (handles expired lockouts)
--     - Stamp LastLoginDateUtc with current UTC time
--
--   Failure path (@LoginSucceeded = 0):
--     - Increment FailedLoginAttempts by 1
--     - If the new count >= @MaxFailedAttempts, set IsLocked = 1
--       and schedule LockoutEndDateUtc = NOW + @LockoutDurationMinutes
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_UpdateLogin
    @UserId                 BIGINT,
    @LoginSucceeded         BIT,
    @MaxFailedAttempts      INT,
    @LockoutDurationMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    IF @LoginSucceeded = 1
    BEGIN
        UPDATE MyMoney.Users
        SET
            FailedLoginAttempts = 0,
            IsLocked            = 0,
            LockoutEndDateUtc   = NULL,
            LastLoginDateUtc    = GETUTCDATE()
        WHERE UserId = @UserId;
    END
    ELSE
    BEGIN
        UPDATE MyMoney.Users
        SET
            FailedLoginAttempts = FailedLoginAttempts + 1,
            IsLocked            = CASE
                                      WHEN FailedLoginAttempts + 1 >= @MaxFailedAttempts THEN 1
                                      ELSE IsLocked
                                  END,
            LockoutEndDateUtc   = CASE
                                      WHEN FailedLoginAttempts + 1 >= @MaxFailedAttempts
                                          THEN DATEADD(MINUTE, @LockoutDurationMinutes, GETUTCDATE())
                                      ELSE LockoutEndDateUtc
                                  END
        WHERE UserId = @UserId;
    END
END;
GO
