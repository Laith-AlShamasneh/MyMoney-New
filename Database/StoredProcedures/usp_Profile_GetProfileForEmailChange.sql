-- ============================================================
-- Stored Procedure: MyMoney.usp_Profile_GetProfileForEmailChange
-- Description: Returns password hash and active state for email change validation.
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Profile_GetProfileForEmailChange
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.PasswordHash,
        u.IsActive,
        p.DisplayNameEn,
        p.DisplayNameAr,
        u.Email
    FROM   MyMoney.Users   u
    JOIN   MyMoney.Persons p ON p.PersonId = u.PersonId
    WHERE  u.UserId = @UserId;
END;
GO
