-- ============================================================
-- Procedure : MyMoney.usp_Category_GetList
-- Description: Returns active categories, optionally filtered
--              by transaction type (1=Income, 2=Expense).
-- ============================================================
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Category_GetList]
    @TypeId TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CategoryId,
        NameEn,
        NameAr,
        TransactionTypeId,
        IconFileName,
        SortOrder
    FROM MyMoney.Categories
    WHERE IsActive = 1
      AND (@TypeId IS NULL OR TransactionTypeId = @TypeId)
    ORDER BY TransactionTypeId ASC, SortOrder ASC, NameEn ASC;
END
GO
