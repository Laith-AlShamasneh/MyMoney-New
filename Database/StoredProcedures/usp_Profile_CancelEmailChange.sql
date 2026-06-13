-- ============================================================
-- Stored Procedure: MyMoney.usp_Profile_CancelEmailChange
-- Description: Deletes any pending email change request for the user.
-- ============================================================
CREATE OR ALTER PROCEDURE MyMoney.usp_Profile_CancelEmailChange
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM MyMoney.EmailChangeTokens
    WHERE  UserId    = @UserId
      AND  UsedOnUtc IS NULL;
END;
GO
