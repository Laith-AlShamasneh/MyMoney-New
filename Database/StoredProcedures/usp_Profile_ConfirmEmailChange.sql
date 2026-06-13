-- ============================================================
-- Stored Procedure: MyMoney.usp_Profile_ConfirmEmailChange
-- Description: Validates the email change token, updates the user's email,
--              marks the token used, and revokes all refresh tokens
--              (because the primary credential — email — changed).
--
-- ResultCode values:
--   0 = Success
--   1 = TokenNotFound or already used
--   2 = TokenExpired
--   3 = NewEmailAlreadyTaken
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Profile_ConfirmEmailChange
    @TokenHash NVARCHAR(64),
    @UsedByIp  NVARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Id           BIGINT;
    DECLARE @UserId       BIGINT;
    DECLARE @NewEmail     NVARCHAR(254);
    DECLARE @OldEmail     NVARCHAR(254);
    DECLARE @ExpiresAtUtc DATETIME2(0);
    DECLARE @UsedOnUtc    DATETIME2(0);
    DECLARE @DisplayNameEn NVARCHAR(200);
    DECLARE @DisplayNameAr NVARCHAR(200);

    SELECT
        @Id           = ect.Id,
        @UserId       = ect.UserId,
        @NewEmail     = ect.NewEmail,
        @ExpiresAtUtc = ect.ExpiresAtUtc,
        @UsedOnUtc    = ect.UsedOnUtc,
        @OldEmail     = u.Email,
        @DisplayNameEn = p.DisplayNameEn,
        @DisplayNameAr = p.DisplayNameAr
    FROM   MyMoney.EmailChangeTokens ect
    JOIN   MyMoney.Users             u ON u.UserId   = ect.UserId
    JOIN   MyMoney.Persons           p ON p.PersonId = u.PersonId
    WHERE  ect.TokenHash = @TokenHash;

    -- Not found or already used
    IF @Id IS NULL OR @UsedOnUtc IS NOT NULL
    BEGIN
        SELECT CAST(1 AS TINYINT) AS ResultCode,
               NULL AS UserId, NULL AS OldEmail, NULL AS NewEmail,
               NULL AS DisplayNameEn, NULL AS DisplayNameAr;
        RETURN;
    END

    -- Expired
    IF @ExpiresAtUtc <= GETUTCDATE()
    BEGIN
        SELECT CAST(2 AS TINYINT) AS ResultCode,
               NULL AS UserId, NULL AS OldEmail, NULL AS NewEmail,
               NULL AS DisplayNameEn, NULL AS DisplayNameAr;
        RETURN;
    END

    -- New email already taken (race condition check)
    IF EXISTS (SELECT 1 FROM MyMoney.Users WHERE Email = @NewEmail AND UserId <> @UserId)
    BEGIN
        SELECT CAST(3 AS TINYINT) AS ResultCode,
               NULL AS UserId, NULL AS OldEmail, NULL AS NewEmail,
               NULL AS DisplayNameEn, NULL AS DisplayNameAr;
        RETURN;
    END

    BEGIN TRANSACTION;

    -- Update email
    UPDATE MyMoney.Users
    SET    Email     = @NewEmail,
           UpdatedAt = GETUTCDATE()
    WHERE  UserId = @UserId;

    -- Mark token used
    UPDATE MyMoney.EmailChangeTokens
    SET    UsedOnUtc = GETUTCDATE(),
           UsedByIp  = @UsedByIp
    WHERE  Id = @Id;

    -- Revoke ALL refresh tokens — email changed = security-critical event
    UPDATE MyMoney.RefreshTokens
    SET    RevokedOnUtc  = GETUTCDATE(),
           RevokedByIp   = @UsedByIp,
           ReasonRevoked = 'EmailChanged'
    WHERE  UserId       = @UserId
      AND  RevokedOnUtc IS NULL
      AND  ExpiresOnUtc > GETUTCDATE();

    COMMIT TRANSACTION;

    SELECT CAST(0 AS TINYINT) AS ResultCode,
           @UserId        AS UserId,
           @OldEmail      AS OldEmail,
           @NewEmail      AS NewEmail,
           @DisplayNameEn AS DisplayNameEn,
           @DisplayNameAr AS DisplayNameAr;
END;
GO
