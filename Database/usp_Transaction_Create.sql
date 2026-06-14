-- ============================================================
-- Procedure : MyMoney.usp_Transaction_Create
-- Description: Inserts a new transaction.
--   @NewId = 0  → category/type mismatch (rejected)
--   @NewId > 0  → success; new TransactionId
-- ============================================================
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Transaction_Create]
    @UserId            BIGINT,
    @CategoryId        INT,
    @TransactionTypeId TINYINT,
    @Amount            DECIMAL(18,2),
    @Description       NVARCHAR(500)  = NULL,
    @TransactionDate   DATE,
    @Notes             NVARCHAR(1000) = NULL,
    @NewId             BIGINT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Category must exist, be active, and match the requested transaction type
    IF NOT EXISTS (
        SELECT 1 FROM MyMoney.Categories
        WHERE CategoryId        = @CategoryId
          AND TransactionTypeId = @TransactionTypeId
          AND IsActive          = 1
    )
    BEGIN
        SET @NewId = 0;
        RETURN;
    END

    INSERT INTO MyMoney.Transactions
        (UserId, CategoryId, TransactionTypeId, Amount, Description, TransactionDate, Notes, IsActive, CreatedAt, CreatedBy)
    VALUES
        (@UserId, @CategoryId, @TransactionTypeId, @Amount, @Description, @TransactionDate, @Notes, 1, GETUTCDATE(), @UserId);

    SET @NewId = SCOPE_IDENTITY();
END
GO
