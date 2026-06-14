-- ============================================================
-- Procedure : MyMoney.usp_Transaction_Delete
-- Description: Soft-deletes a transaction owned by @UserId.
--   @AffectedRows = 0 → not found or wrong owner
--   @AffectedRows = 1 → success
-- ============================================================
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Transaction_Delete]
    @UserId        BIGINT,
    @TransactionId BIGINT,
    @AffectedRows  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE MyMoney.Transactions
    SET IsActive  = 0,
        UpdatedAt = GETUTCDATE(),
        UpdatedBy = @UserId
    WHERE TransactionId = @TransactionId
      AND UserId        = @UserId
      AND IsActive      = 1;

    SET @AffectedRows = @@ROWCOUNT;
END
GO
