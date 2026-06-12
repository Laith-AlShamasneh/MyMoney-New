-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_GetUserForChangePassword
-- Description: Returns the user's current password hash and account state
--              for the authenticated change-password flow.
--              Keyed by UserId (from JWT claims) — not by email.
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_GetUserForChangePassword
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.PasswordHash,
        u.IsActive,
        p.DisplayNameEn,
        p.DisplayNameAr
    FROM   MyMoney.Users   u
    INNER JOIN MyMoney.Persons p ON p.PersonId = u.PersonId
    WHERE  u.UserId    = @UserId
      AND  u.IsDeleted = 0;
END;
GO
