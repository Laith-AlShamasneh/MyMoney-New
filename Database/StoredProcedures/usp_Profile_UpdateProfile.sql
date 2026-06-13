-- ============================================================
-- Stored Procedure: MyMoney.usp_Profile_UpdateProfile
-- Description: Updates editable Persons fields for a user.
--
-- ResultCode values:
--   0 = Success
--   1 = UserNotFound
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Profile_UpdateProfile
    @UserId       BIGINT,
    @FirstNameEn  NVARCHAR(100),
    @LastNameEn   NVARCHAR(100),
    @FirstNameAr  NVARCHAR(100) = NULL,
    @LastNameAr   NVARCHAR(100) = NULL,
    @DisplayNameEn NVARCHAR(200),
    @DisplayNameAr NVARCHAR(200) = NULL,
    @DateOfBirth  DATE          = NULL,
    @GenderId     TINYINT       = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PersonId BIGINT;

    SELECT @PersonId = PersonId
    FROM   MyMoney.Users
    WHERE  UserId = @UserId AND IsActive = 1;

    IF @PersonId IS NULL
    BEGIN
        SELECT CAST(1 AS TINYINT) AS ResultCode;
        RETURN;
    END

    UPDATE MyMoney.Persons
    SET    FirstNameEn  = @FirstNameEn,
           LastNameEn   = @LastNameEn,
           FirstNameAr  = @FirstNameAr,
           LastNameAr   = @LastNameAr,
           DisplayNameEn = @DisplayNameEn,
           DisplayNameAr = @DisplayNameAr,
           DateOfBirth  = @DateOfBirth,
           GenderId     = @GenderId,
           UpdatedAt    = GETUTCDATE(),
           UpdatedBy    = @UserId
    WHERE  PersonId = @PersonId;

    SELECT CAST(0 AS TINYINT) AS ResultCode;
END;
GO
