-- ============================================================
-- Stored Procedure: MyMoney.usp_Profile_RemoveProfilePicture
-- Description: Sets ProfilePicture to NULL; returns old filename for cleanup.
--
-- ResultCode values:
--   0 = Success
--   1 = UserNotFound
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Profile_RemoveProfilePicture
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PersonId   BIGINT;
    DECLARE @OldPicture NVARCHAR(500);

    SELECT @PersonId   = u.PersonId,
           @OldPicture = p.ProfilePicture
    FROM   MyMoney.Users   u
    JOIN   MyMoney.Persons p ON p.PersonId = u.PersonId
    WHERE  u.UserId = @UserId AND u.IsActive = 1;

    IF @PersonId IS NULL
    BEGIN
        SELECT CAST(1 AS TINYINT) AS ResultCode, NULL AS OldProfilePicture;
        RETURN;
    END

    UPDATE MyMoney.Persons
    SET    ProfilePicture = NULL,
           UpdatedAt      = GETUTCDATE(),
           UpdatedBy      = @UserId
    WHERE  PersonId = @PersonId;

    SELECT CAST(0 AS TINYINT) AS ResultCode, @OldPicture AS OldProfilePicture;
END;
GO
