-- ============================================================
-- Procedure : MyMoney.usp_Transaction_Update
-- Description: Updates a transaction owned by @UserId.
--   @AffectedRows = 0 → not found / wrong owner / category mismatch
--   @AffectedRows = 1 → success
-- ============================================================
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Transaction_Update]
    @UserId            BIGINT,
    @TransactionId     BIGINT,
    @CategoryId        INT,
    @TransactionTypeId TINYINT,
    @Amount            DECIMAL(18,2),
    @Description       NVARCHAR(500)  = NULL,
    @TransactionDate   DATE,
    @Notes             NVARCHAR(1000) = NULL,
    @AffectedRows      INT            OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Validate category/type pairing before touching the row
    IF NOT EXISTS (
        SELECT 1 FROM MyMoney.Categories
        WHERE CategoryId        = @CategoryId
          AND TransactionTypeId = @TransactionTypeId
          AND IsActive          = 1
    )
    BEGIN
        SET @AffectedRows = 0;
        RETURN;
    END

    UPDATE MyMoney.Transactions
    SET
        CategoryId        = @CategoryId,
        TransactionTypeId = @TransactionTypeId,
        Amount            = @Amount,
        Description       = @Description,
        TransactionDate   = @TransactionDate,
        Notes             = @Notes,
        UpdatedAt         = GETUTCDATE(),
        UpdatedBy         = @UserId
    WHERE TransactionId = @TransactionId
      AND UserId        = @UserId
      AND IsActive      = 1;

    SET @AffectedRows = @@ROWCOUNT;
END
GO
