-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_GetUserConfirmationStatus
-- Description: Returns a user's confirmation state for the resend-confirmation flow.
--              Returns no row if the email does not exist or the account is deleted.
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_GetUserConfirmationStatus
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.IsEmailConfirmed,
        u.IsActive,
        p.DisplayNameEn,
        p.DisplayNameAr
    FROM   MyMoney.Users   u
    INNER JOIN MyMoney.Persons p ON p.PersonId = u.PersonId
    WHERE  u.Email     = @Email
      AND  u.IsDeleted = 0;
END;
GO
