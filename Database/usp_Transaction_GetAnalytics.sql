-- ============================================================
-- Procedure : MyMoney.usp_Transaction_GetAnalytics
-- Description: Returns two result sets:
--   RS1 – expense category breakdown (filtered by date range)
--   RS2 – 12-month income vs. expense trend
-- ============================================================
CREATE OR ALTER PROCEDURE [MyMoney].[usp_Transaction_GetAnalytics]
    @UserId   BIGINT,
    @DateFrom DATE = NULL,
    @DateTo   DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- ── RS1: Expense breakdown by category ───────────────────────────────────
    DECLARE @TotalExp DECIMAL(18,2);
    SELECT @TotalExp = ISNULL(SUM(T.Amount), 0)
    FROM MyMoney.Transactions AS T
    WHERE T.UserId            = @UserId
      AND T.IsActive          = 1
      AND T.TransactionTypeId = 2
      AND (@DateFrom IS NULL OR T.TransactionDate >= @DateFrom)
      AND (@DateTo   IS NULL OR T.TransactionDate <= @DateTo);

    SELECT
        C.CategoryId,
        C.NameEn,
        C.NameAr,
        SUM(T.Amount)                                              AS TotalAmount,
        COUNT(*)                                                   AS TxCount,
        ROUND(SUM(T.Amount) * 100.0 / NULLIF(@TotalExp, 0), 1)    AS Percentage
    FROM MyMoney.Transactions AS T
    INNER JOIN MyMoney.Categories AS C ON C.CategoryId = T.CategoryId
    WHERE T.UserId            = @UserId
      AND T.IsActive          = 1
      AND T.TransactionTypeId = 2
      AND (@DateFrom IS NULL OR T.TransactionDate >= @DateFrom)
      AND (@DateTo   IS NULL OR T.TransactionDate <= @DateTo)
    GROUP BY C.CategoryId, C.NameEn, C.NameAr
    HAVING SUM(T.Amount) > 0
    ORDER BY TotalAmount DESC;

    -- ── RS2: 12-month income vs. expense trend ───────────────────────────────
    DECLARE @TrendStart DATE =
        DATEADD(MONTH, -11, DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1));

    SELECT
        YEAR(T.TransactionDate)  AS Year,
        MONTH(T.TransactionDate) AS Month,
        ISNULL(SUM(CASE WHEN T.TransactionTypeId = 1 THEN T.Amount ELSE 0 END), 0) AS Income,
        ISNULL(SUM(CASE WHEN T.TransactionTypeId = 2 THEN T.Amount ELSE 0 END), 0) AS Expenses
    FROM MyMoney.Transactions AS T
    WHERE T.UserId   = @UserId
      AND T.IsActive = 1
      AND T.TransactionDate >= @TrendStart
    GROUP BY YEAR(T.TransactionDate), MONTH(T.TransactionDate)
    ORDER BY Year ASC, Month ASC;
END
GO
