-- =============================================================================
-- MyMoney.usp_Authentication_RefreshToken
-- Atomically rotates a refresh token.
--
-- Validates the old token, revokes it, inserts the new one, and returns the
-- user's profile data in a single round-trip so the application can build
-- a fresh JWT + refresh-token response without extra lookups.
--
-- ResultCode:
--   0 = Success        — old token revoked, new token inserted, user data returned
--   1 = Invalid        — token not found, already revoked, or user inactive/deleted
--   2 = Expired        — token was valid but past its ExpiresOnUtc
-- =============================================================================

CREATE OR ALTER PROCEDURE MyMoney.usp_Authentication_RefreshToken
    @OldTokenHash       NVARCHAR(512),
    @NewTokenHash       NVARCHAR(512),
    @NewExpiresOnUtc    DATETIME2(0),
    @RevokedByIp        NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- ── 1. Look up the old token ──────────────────────────────────────────────
    DECLARE
        @UserId         BIGINT,
        @RevokedOnUtc   DATETIME2(0),
        @ExpiresOnUtc   DATETIME2(0);

    SELECT
        @UserId       = rt.UserId,
        @RevokedOnUtc = rt.RevokedOnUtc,
        @ExpiresOnUtc = rt.ExpiresOnUtc
    FROM MyMoney.RefreshTokens rt
    WHERE rt.Token = @OldTokenHash;

    -- ── 2. Token not found ────────────────────────────────────────────────────
    IF @UserId IS NULL
    BEGIN
        SELECT
            1               AS ResultCode,
            CAST(0 AS BIGINT) AS UserId,
            NULL            AS Email,
            NULL            AS DisplayNameEn,
            NULL            AS DisplayNameAr,
            NULL            AS ProfilePicture,
            CAST(0 AS INT)  AS RoleId,
            NULL            AS RoleNameEn,
            NULL            AS RoleNameAr;
        RETURN;
    END

    -- ── 3. Already revoked ────────────────────────────────────────────────────
    IF @RevokedOnUtc IS NOT NULL
    BEGIN
        SELECT
            1               AS ResultCode,
            CAST(0 AS BIGINT) AS UserId,
            NULL            AS Email,
            NULL            AS DisplayNameEn,
            NULL            AS DisplayNameAr,
            NULL            AS ProfilePicture,
            CAST(0 AS INT)  AS RoleId,
            NULL            AS RoleNameEn,
            NULL            AS RoleNameAr;
        RETURN;
    END

    -- ── 4. Expired ────────────────────────────────────────────────────────────
    IF @ExpiresOnUtc < GETUTCDATE()
    BEGIN
        SELECT
            2               AS ResultCode,
            CAST(0 AS BIGINT) AS UserId,
            NULL            AS Email,
            NULL            AS DisplayNameEn,
            NULL            AS DisplayNameAr,
            NULL            AS ProfilePicture,
            CAST(0 AS INT)  AS RoleId,
            NULL            AS RoleNameEn,
            NULL            AS RoleNameAr;
        RETURN;
    END

    -- ── 5. Verify user is still active ───────────────────────────────────────
    IF NOT EXISTS (
        SELECT 1
        FROM MyMoney.Users
        WHERE UserId    = @UserId
          AND IsActive  = 1
          AND IsDeleted = 0
    )
    BEGIN
        SELECT
            1               AS ResultCode,
            CAST(0 AS BIGINT) AS UserId,
            NULL            AS Email,
            NULL            AS DisplayNameEn,
            NULL            AS DisplayNameAr,
            NULL            AS ProfilePicture,
            CAST(0 AS INT)  AS RoleId,
            NULL            AS RoleNameEn,
            NULL            AS RoleNameAr;
        RETURN;
    END

    -- ── 6. Rotate: revoke old, insert new ────────────────────────────────────
    UPDATE MyMoney.RefreshTokens
    SET
        RevokedOnUtc    = GETUTCDATE(),
        RevokedByIp     = @RevokedByIp,
        ReasonRevoked   = 'Rotation',
        ReplacedByToken = @NewTokenHash
    WHERE Token = @OldTokenHash;

    INSERT INTO MyMoney.RefreshTokens (UserId, Token, ExpiresOnUtc, CreatedOnUtc, CreatedByIp)
    VALUES (@UserId, @NewTokenHash, @NewExpiresOnUtc, GETUTCDATE(), @RevokedByIp);

    -- ── 7. Return user data for response building ─────────────────────────────
    SELECT
        0               AS ResultCode,
        u.UserId,
        u.Email,
        p.DisplayNameEn,
        p.DisplayNameAr,
        p.ProfilePicture,
        ur.RoleId,
        r.NameEn        AS RoleNameEn,
        r.NameAr        AS RoleNameAr
    FROM        MyMoney.Users       u
    INNER JOIN  MyMoney.Persons     p   ON  p.PersonId  = u.PersonId
    INNER JOIN  MyMoney.UserRoles   ur  ON  ur.UserId   = u.UserId
    INNER JOIN  MyMoney.Roles       r   ON  r.RoleId    = ur.RoleId
    WHERE u.UserId = @UserId;
END;
GO
