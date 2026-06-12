-- ============================================================
-- Stored Procedure: MyMoney.usp_Authentication_Login
-- Description: Retrieves full user record for login validation.
--              Returns NULL row if email not found or account deleted.
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_Login
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.PersonId,
        u.Email,
        p.DisplayNameEn,
        p.DisplayNameAr,
        p.ProfilePicture,
        u.PasswordHash,
        u.IsActive,
        u.IsEmailConfirmed,
        u.IsLocked,
        u.LockoutEndDateUtc,
        u.FailedLoginAttempts,
        ur.RoleId,
        r.NameEn    AS RoleNameEn,
        r.NameAr    AS RoleNameAr
    FROM        MyMoney.Users       u
    INNER JOIN  MyMoney.Persons     p   ON  p.PersonId  = u.PersonId
    INNER JOIN  MyMoney.UserRoles   ur  ON  ur.UserId   = u.UserId
    INNER JOIN  MyMoney.Roles       r   ON  r.RoleId    = ur.RoleId
    WHERE   u.Email     = @Email        -- SQL Server collation handles case-insensitivity
      AND   u.IsDeleted = 0;
END;
GO
