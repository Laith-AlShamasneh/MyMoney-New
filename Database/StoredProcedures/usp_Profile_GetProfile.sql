-- ============================================================
-- Stored Procedure: MyMoney.usp_Profile_GetProfile
-- Description: Returns the full profile for an authenticated user,
--              including any pending email change request.
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Profile_GetProfile
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.PersonId,
        p.FirstNameEn,
        p.LastNameEn,
        p.FirstNameAr,
        p.LastNameAr,
        p.DisplayNameEn,
        p.DisplayNameAr,
        u.Email,
        p.DateOfBirth,
        p.GenderId,
        p.ProfilePicture,
        u.IsEmailConfirmed,
        ect.NewEmail    AS PendingEmail
    FROM   MyMoney.Users    u
    JOIN   MyMoney.Persons  p ON p.PersonId = u.PersonId
    LEFT JOIN MyMoney.EmailChangeTokens ect
           ON ect.UserId     = u.UserId
          AND ect.UsedOnUtc  IS NULL
          AND ect.ExpiresAtUtc > GETUTCDATE()
    WHERE  u.UserId   = @UserId
      AND  u.IsActive = 1;
END;
GO
