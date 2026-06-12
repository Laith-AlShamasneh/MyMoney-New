-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_GetUserForPasswordReset
-- Description: Returns the minimum user information needed to initiate
--              a password reset. Returns no row if the email does not
--              exist or the account is deleted — the caller always returns
--              the same success response (no user enumeration).
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_GetUserForPasswordReset
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.IsActive,
        p.DisplayNameEn,
        p.DisplayNameAr
    FROM   MyMoney.Users   u
    INNER JOIN MyMoney.Persons p ON p.PersonId = u.PersonId
    WHERE  u.Email     = @Email
      AND  u.IsDeleted = 0;
END;
GO
