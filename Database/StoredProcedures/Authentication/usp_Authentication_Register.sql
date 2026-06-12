-- =============================================================================
-- MyMoney.usp_Authentication_Register
-- Creates Person, User, and UserRole records atomically in a single transaction.
-- Returns the newly created user's data on success, or no rows if a duplicate
-- email was detected (race condition after the application-level check).
-- Emails are stored lower-cased for case-insensitive uniqueness.
-- =============================================================================

CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_Register
    -- Person
    @FirstNameEn    NVARCHAR(100),
    @LastNameEn     NVARCHAR(100),
    @FirstNameAr    NVARCHAR(100)   = NULL,
    @LastNameAr     NVARCHAR(100)   = NULL,
    @DisplayNameEn  NVARCHAR(200),
    @DisplayNameAr  NVARCHAR(200)   = NULL,
    @DateOfBirth    DATE            = NULL,
    @GenderId       TINYINT         = NULL,
    @ProfilePicture NVARCHAR(500)   = NULL,
    -- User
    @Email          NVARCHAR(256),
    @PasswordHash   NVARCHAR(512),
    -- Role
    @DefaultRoleId  INT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Race-condition guard: return no rows if email was registered between
        -- the application-level check and this SP execution.
        IF EXISTS (SELECT 1 FROM MyMoney.Users WHERE Email = LOWER(@Email))
        BEGIN
            ROLLBACK TRANSACTION;
            RETURN;
        END

        -- Create Person
        INSERT INTO MyMoney.Persons
            (FirstNameEn, LastNameEn, FirstNameAr, LastNameAr,
             DisplayNameEn, DisplayNameAr, DateOfBirth, GenderId, ProfilePicture)
        VALUES
            (@FirstNameEn, @LastNameEn, @FirstNameAr, @LastNameAr,
             @DisplayNameEn, @DisplayNameAr, @DateOfBirth, @GenderId, @ProfilePicture);

        DECLARE @PersonId BIGINT = SCOPE_IDENTITY();

        -- Create User (email stored lowercase; LastLoginDateUtc set at registration)
        INSERT INTO MyMoney.Users
            (PersonId, Email, PasswordHash, LastLoginDateUtc)
        VALUES
            (@PersonId, LOWER(@Email), @PasswordHash, GETUTCDATE());

        DECLARE @UserId BIGINT = SCOPE_IDENTITY();

        -- Assign default role
        INSERT INTO MyMoney.UserRoles (UserId, RoleId)
        VALUES (@UserId, @DefaultRoleId);

        COMMIT TRANSACTION;

        -- Return the new user record with role info
        SELECT
            u.UserId,
            p.PersonId,
            u.Email,
            p.DisplayNameEn,
            p.DisplayNameAr,
            p.ProfilePicture,
            r.RoleId,
            r.NameEn    AS RoleNameEn,
            r.NameAr    AS RoleNameAr
        FROM  MyMoney.Users     u
        JOIN  MyMoney.Persons   p  ON p.PersonId = u.PersonId
        JOIN  MyMoney.UserRoles ur ON ur.UserId   = u.UserId
        JOIN  MyMoney.Roles     r  ON r.RoleId    = ur.RoleId
        WHERE u.UserId = @UserId;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
