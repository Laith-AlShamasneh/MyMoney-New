/* =============================================================================
   Audit remediation H8 — access-token revocation via per-user SecurityStamp
   Target schema : MyMoney DB v12+
   Apply ORDER   : apply this BEFORE setting Jwt:ValidateSecurityStamp = true in
                   the API. The .NET can ship first with the flag OFF (no DB
                   dependency); flip the flag only once this migration is applied.
   Idempotent    : safe to re-run; self-registers in MyMoney.SchemaMigrations.

   What it does
   ------------
   Adds Users.SecurityStamp (a value embedded in every access token as the
   'sstamp' claim). The API validates the claim against the current stamp on each
   request (cached); bumping the stamp instantly invalidates all outstanding
   access tokens for that user. The stamp is bumped on password change and can be
   bumped on "log out everywhere". Two tiny helper SPs avoid touching the large
   Login / RefreshToken / Register procedures.
   ============================================================================= */
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('MyMoney.Users') AND name = 'SecurityStamp')
BEGIN
    -- NOT NULL + DEFAULT backfills every existing row with a fresh stamp in place.
    ALTER TABLE MyMoney.Users
        ADD SecurityStamp UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_Users_SecurityStamp DEFAULT NEWID();
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Authentication_GetSecurityStamp]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT SecurityStamp FROM MyMoney.Users WHERE UserId = @UserId;
END
GO

CREATE OR ALTER PROCEDURE [MyMoney].[usp_Authentication_BumpSecurityStamp]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE MyMoney.Users
    SET    SecurityStamp = NEWID(),
           UpdatedAt     = GETUTCDATE()
    WHERE  UserId = @UserId;
END
GO

IF NOT EXISTS (SELECT 1 FROM MyMoney.SchemaMigrations WHERE ScriptName = 'audit-h8-security-stamp.sql')
    INSERT INTO MyMoney.SchemaMigrations (ScriptName, AppliedAtUtc, Notes)
    VALUES ('audit-h8-security-stamp.sql', GETUTCDATE(),
            'H8: Users.SecurityStamp + Get/Bump SPs for access-token revocation');
GO
