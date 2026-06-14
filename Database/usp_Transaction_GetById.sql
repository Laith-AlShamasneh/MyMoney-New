-- ============================================================
-- Procedure : MyMoney.usp_Transaction_GetById
-- Description: Returns a single transaction row for the given
--              user (ownership enforced). Returns empty if not
--              found, deleted, or owned by another user.
-- ============================================================
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Transaction_GetById]
    @UserId        BIGINT,
    @TransactionId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        T.TransactionId,
        T.CategoryId,
        C.NameEn        AS CategoryNameEn,
        C.NameAr        AS CategoryNameAr,
        C.IconFileName  AS CategoryIcon,
        T.TransactionTypeId,
        T.Amount,
        T.Description,
        T.TransactionDate,
        T.Notes,
        T.CreatedAt,
        T.UpdatedAt
    FROM MyMoney.Transactions AS T
    INNER JOIN MyMoney.Categories AS C ON C.CategoryId = T.CategoryId
    WHERE T.TransactionId = @TransactionId
      AND T.UserId        = @UserId
      AND T.IsActive      = 1;
END
GO
