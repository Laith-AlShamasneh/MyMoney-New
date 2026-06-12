-- =============================================================================
-- MyMoney.usp_Authentication_CheckEmailExists
-- Returns a single BIT column: 1 if the email is already registered, 0 if not.
-- Email comparison is case-insensitive (stored as LOWER).
-- =============================================================================

CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_CheckEmailExists
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CAST(
        CASE WHEN EXISTS (
            SELECT 1 FROM MyMoney.Users WHERE Email = LOWER(@Email)
        ) THEN 1 ELSE 0 END
    AS BIT) AS EmailExists;
END
GO
